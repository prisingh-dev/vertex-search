import json
from pathlib import Path
import re
from typing import Any

from google.cloud.retail_v2 import (
    CreateServingConfigRequest,
    ServingConfigServiceClient,
    CreateControlRequest,
    ControlServiceClient,
    ProductServiceClient,
    ServingConfig,
    SolutionType,
    Control,
)
from google.cloud.retail_v2.types import serving_config_service
from google.api_core.exceptions import (
    AlreadyExists,
    NotFound,
    InvalidArgument,
)
from google.protobuf import field_mask_pb2
from google.protobuf.json_format import MessageToDict
from os import environ

# ---------------------------------------------------------------------------
# Configuration — loaded from environment variables.
# Set these by sourcing the appropriate config file before running, or via
# the Makefile targets (e.g. `make -f serving/Makefile apply-dev`).
# ---------------------------------------------------------------------------

project_id = environ.get("PROJECT_ID")
if not project_id:
    raise EnvironmentError(
        "PROJECT_ID environment variable is not set. "
        "Source the appropriate config file or use a Makefile target."
    )

location = environ.get("LOCATION", "global")  # Retail API is usually 'global'
catalog_id = environ.get(
    "CATALOG_ID", "default_catalog"
)  # 'default_catalog' is the standard default
agent_config_id = environ.get("AGENT_CONFIG_ID", "agent-serving-config")

# Set quota project to the same project so API costs are attributed correctly
environ["GOOGLE_CLOUD_QUOTA_PROJECT"] = project_id

parent = f"projects/{project_id}/locations/{location}/catalogs/{catalog_id}"

FACET_ATTRIBUTE_ALIASES: dict[str, str] = {
    "color": "colorFamilies",
    "size": "sizes",
    "brand": "brands",
    "category": "categories",
    "price": "price",
}

def _build_legacy_filter_condition(control_data: dict[str, Any]) -> str:
    attribute = control_data["attribute"]
    condition = control_data["condition"]
    entities = control_data["entities"]
    filter_entities = '\",\"'.join(entities)
    return f'{condition} {attribute}: ANY("{filter_entities}")'


def _extract_query_terms(condition_value: Any) -> list[dict[str, Any]]:
    if not isinstance(condition_value, str):
        return []

    text = condition_value.strip()
    if not text:
        return []

    if text.lower().startswith("query:"):
        query_term = text.split(":", 1)[1].strip()
        return [{"value": query_term}] if query_term else []

    return []


def _normalize_query_value(raw_query_condition: str) -> str:
    text = raw_query_condition.strip()
    if text.lower().startswith("query:"):
        text = text.split(":", 1)[1].strip()
    return text


def _build_query_condition(query_condition: str, match_type: str, control_id: str) -> dict[str, Any]:
    normalized_match_type = match_type.strip().upper()
    if normalized_match_type not in {"FULL", "PARTIAL"}:
        raise ValueError(
            f"Control '{control_id}' has invalid matchType '{match_type}'. "
            "Supported values are FULL and PARTIAL."
        )

    query_value = _normalize_query_value(query_condition)
    if not query_value:
        raise ValueError(
            f"Control '{control_id}' has invalid queryCondition '{query_condition}'."
        )

    return {
        "query_terms": [
            {
                "value": query_value,
                "full_match": normalized_match_type == "FULL",
            }
        ]
    }


def _ensure_rule_condition(payload: dict[str, Any], fallback_condition: Any = None) -> dict[str, Any]:
    rule = payload.setdefault("rule", {})
    if not isinstance(rule, dict):
        raise ValueError("Invalid Retail control payload: 'rule' must be an object")

    condition = rule.get("condition")
    if isinstance(condition, dict) and condition:
        return payload

    query_terms = _extract_query_terms(fallback_condition)
    rule["condition"] = {"query_terms": query_terms}
    return payload


def _normalize_filter_expression(raw_filter: str) -> str:
    expr = raw_filter.strip()
    if not expr:
        return expr

    if "ANY(" in expr:
        return expr

    # Convert shorthand like "brand:Acme" to "brand: ANY(\"Acme\")".
    match = re.fullmatch(r"([A-Za-z0-9_.]+)\s*:\s*(.+)", expr)
    if not match:
        # Convert string equality into ANY() syntax, e.g.
        # availability = 'IN_STOCK' -> availability: ANY("IN_STOCK")
        # Keep numeric equality as-is.
        def _replace_string_equals(match_obj: re.Match[str]) -> str:
            field_name = match_obj.group(1)
            quoted_value = match_obj.group(2)
            bare_value = match_obj.group(3)
            value = quoted_value if quoted_value is not None else bare_value
            value = (value or "").strip()

            if not value:
                return match_obj.group(0)

            # Do not rewrite numeric equality comparisons.
            if re.fullmatch(r"[+-]?\d+(?:\.\d+)?", value):
                return match_obj.group(0)

            return f'{field_name}: ANY("{value}")'

        return re.sub(
            r"\b([A-Za-z0-9_.]+)\s*=\s*(?:'([^']*)'|\"([^\"]*)\"|([A-Za-z_][A-Za-z0-9_\-]*))",
            _replace_string_equals,
            expr,
        )

    field_name = match.group(1).strip()
    raw_value = match.group(2).strip().strip('"').strip("'")
    if not raw_value:
        return expr

    return f'{field_name}: ANY("{raw_value}")'


def _normalize_facet_attribute_name(name: str) -> str:
    alias_key = name.strip().lower()
    return FACET_ATTRIBUTE_ALIASES.get(alias_key, name)


def _extract_product_id_from_resource_name(resource_name: str) -> str:
    if "/products/" in resource_name:
        return resource_name.rsplit("/products/", 1)[1].strip()
    return resource_name.strip()


def _escape_filter_value(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def _find_product_ids_by_title(product_service_client: ProductServiceClient, title: str) -> list[str]:
    expected_title = title.strip().lower()
    if not expected_title:
        return []

    matched_ids: set[str] = set()

    def _collect_matches(pager: Any, max_scan: int | None = None) -> None:
        scanned = 0
        for product in pager:
            scanned += 1
            if max_scan is not None and scanned > max_scan:
                break

            product_title = str(getattr(product, "title", "") or "").strip().lower()
            if product_title != expected_title:
                continue

            product_id = str(getattr(product, "id", "") or "").strip()
            if not product_id:
                product_id = _extract_product_id_from_resource_name(str(getattr(product, "name", "") or ""))

            if product_id:
                matched_ids.add(product_id)

    escaped_title = _escape_filter_value(title.strip())
    title_filter = f'title: ANY("{escaped_title}")'

    try:
        filtered_pager = product_service_client.list_products(
            parent=parent,
            filter=title_filter,
            page_size=100,
        )
        _collect_matches(filtered_pager)
    except InvalidArgument:
        # Fallback if title filter is unavailable in the current API surface.
        fallback_pager = product_service_client.list_products(parent=parent, page_size=200)
        _collect_matches(fallback_pager, max_scan=1000)

    return sorted(matched_ids)


def _normalize_single_pinned_product(
    control_id: str,
    product_entry: Any,
    product_service_client: ProductServiceClient,
) -> tuple[str, int | None]:
    explicit_position: int | None = None

    if isinstance(product_entry, dict):
        product_id_value = product_entry.get("id")
        product_name_value = product_entry.get("name")
        position_value = product_entry.get("position")

        if position_value is not None:
            if isinstance(position_value, int):
                explicit_position = position_value
            elif isinstance(position_value, str) and position_value.strip().isdigit():
                explicit_position = int(position_value.strip())
            else:
                raise ValueError(
                    f"Control '{control_id}' has invalid pinned product position '{position_value}'. "
                    "Position must be a positive integer."
                )

            if explicit_position <= 0 or explicit_position > 120:
                raise ValueError(
                    f"Control '{control_id}' has invalid pinned product position '{explicit_position}'. "
                    "Position must be between 1 and 120."
                )

        if isinstance(product_id_value, str) and product_id_value.strip():
            product_entry = product_id_value.strip()
        elif isinstance(product_name_value, str) and product_name_value.strip():
            product_entry = product_name_value.strip()
        else:
            raise ValueError(
                f"Control '{control_id}' has invalid pinned product object. "
                "Use {'id': '91869'} or {'name': 'Product Title'} (optionally with 'position')."
            )

    if not isinstance(product_entry, str):
        raise ValueError(
            f"Control '{control_id}' has invalid pinned product entry '{product_entry}'. "
            "Entries must be strings or objects with 'id'/'name'."
        )

    raw_value = product_entry.strip()
    if not raw_value:
        raise ValueError(f"Control '{control_id}' has an empty pinned product entry.")

    if raw_value.startswith("products/"):
        resolved_id = raw_value.split("/", 1)[1].strip()
        if not resolved_id:
            raise ValueError(f"Control '{control_id}' has invalid pinned product value '{raw_value}'.")
        return resolved_id, explicit_position

    if "/" not in raw_value and re.fullmatch(r"[A-Za-z0-9_.:-]+", raw_value):
        return raw_value, explicit_position

    matched_ids = _find_product_ids_by_title(product_service_client, raw_value)
    if not matched_ids:
        raise ValueError(
            f"Control '{control_id}' could not resolve pinned product name '{raw_value}' to a product id."
        )

    if len(matched_ids) > 1:
        preview_ids = ", ".join(matched_ids[:5])
        raise ValueError(
            f"Control '{control_id}' has ambiguous pinned product name '{raw_value}'. "
            f"Matched ids: {preview_ids}. Provide an explicit id instead."
        )

    return matched_ids[0], explicit_position


def _normalize_pinned_products(
    control_id: str,
    pinned_products: list[Any],
    product_service_client: ProductServiceClient,
) -> dict[int, str]:
    resolved_entries = [
        _normalize_single_pinned_product(control_id, entry, product_service_client)
        for entry in pinned_products
    ]

    if len(resolved_entries) > 120:
        raise ValueError(f"Control '{control_id}' has too many pinned products. Maximum allowed is 120.")

    pin_map: dict[int, str] = {}
    used_positions: set[int] = set()
    used_product_ids: set[str] = set()

    # Place explicitly positioned items first.
    for product_id, explicit_position in resolved_entries:
        if explicit_position is None:
            continue

        if explicit_position in used_positions:
            raise ValueError(
                f"Control '{control_id}' has duplicate pin position '{explicit_position}'."
            )

        if product_id in used_product_ids:
            raise ValueError(
                f"Control '{control_id}' has duplicate pinned product id '{product_id}'."
            )

        pin_map[explicit_position] = product_id
        used_positions.add(explicit_position)
        used_product_ids.add(product_id)

    # Fill remaining items by input order using first available positions.
    next_position = 1
    for product_id, explicit_position in resolved_entries:
        if explicit_position is not None:
            continue

        if product_id in used_product_ids:
            raise ValueError(
                f"Control '{control_id}' has duplicate pinned product id '{product_id}'."
            )

        while next_position in used_positions:
            next_position += 1

        if next_position > 120:
            raise ValueError(f"Control '{control_id}' has too many pinned products. Maximum allowed is 120.")

        pin_map[next_position] = product_id
        used_positions.add(next_position)
        used_product_ids.add(product_id)
        next_position += 1

    return pin_map


def _build_synonym_group_controls(
    control_id: str,
    item: dict[str, Any],
) -> dict[str, dict[str, Any]]:
    synonym_spec = item.get("synonymSpec", {})
    synonym_groups = synonym_spec.get("synonyms", [])
    if not isinstance(synonym_groups, list):
        raise ValueError(f"Invalid control '{control_id}': synonymSpec.synonyms must be an array.")

    expanded_controls: dict[str, dict[str, Any]] = {}
    valid_group_count = 0

    for index, synonym_group in enumerate(synonym_groups, start=1):
        if not isinstance(synonym_group, dict):
            raise ValueError(f"Invalid control '{control_id}': each synonym group must be an object.")

        group_terms = synonym_group.get("terms", [])
        if not isinstance(group_terms, list):
            raise ValueError(f"Invalid control '{control_id}': synonym group terms must be an array.")

        clean_terms = list(dict.fromkeys(
            term.strip() for term in group_terms if isinstance(term, str) and term.strip()
        ))
        if not clean_terms:
            continue

        if len(clean_terms) < 2:
            raise ValueError(
                f"Invalid control '{control_id}': each synonym group must contain at least 2 distinct terms."
            )

        valid_group_count += 1
        expanded_control_id = control_id if len(synonym_groups) == 1 else f"{control_id}-{valid_group_count}"
        expanded_item = dict(item)
        expanded_item["synonymSpec"] = {"synonyms": [{"terms": clean_terms}]}
        expanded_controls[expanded_control_id] = {
            "type": expanded_item.get("type"),
            "request_body": {k: v for k, v in expanded_item.items() if k != "controlId"},
        }

    if not expanded_controls:
        raise ValueError(f"Invalid control '{control_id}': synonymSpec.synonyms must contain at least one valid group.")

    return expanded_controls


def _control_rule_dict(control: Control) -> dict[str, Any]:
    if not getattr(control, "rule", None):
        return {}
    return MessageToDict(control.rule._pb, preserving_proto_field_name=True)


def _update_existing_control(
    control_service_client: ControlServiceClient,
    control_name: str,
    desired_control: Control,
) -> bool:
    current_control = control_service_client.get_control(name=control_name)

    update_paths: list[str] = []

    if current_control.display_name != desired_control.display_name:
        current_control.display_name = desired_control.display_name
        update_paths.append("display_name")

    if _control_rule_dict(current_control) != _control_rule_dict(desired_control):
        current_control.rule = desired_control.rule
        update_paths.append("rule")

    if not update_paths:
        return False

    update_mask = field_mask_pb2.FieldMask(paths=update_paths)
    control_service_client.update_control(
        control=current_control,
        update_mask=update_mask,
    )
    return True


def _build_control_payload_from_request(
    control_id: str,
    control_spec: dict[str, Any],
    product_service_client: ProductServiceClient,
) -> dict[str, Any] | None:
    request_body = dict(control_spec.get("request_body", {}))
    control_type = str(control_spec.get("type") or request_body.get("type") or "").upper()

    payload: dict[str, Any] = {
        "display_name": request_body.get("displayName", control_id),
    }

    # If payload already looks like Retail Control schema, use it directly.
    if "rule" in request_body or "display_name" in request_body:
        retail_payload = {k: v for k, v in request_body.items() if k != "type"}
        retail_payload.setdefault("display_name", control_id)
        return _ensure_rule_condition(retail_payload)

    if control_type == "FILTER_CONTROL":
        filter_spec = request_body.get("filterSpec", {})
        filter_expression = filter_spec.get("filterExpression")
        filter_query_condition = (
            filter_spec.get("queryCondition")
            or filter_spec.get("condition")
            or request_body.get("queryCondition")
            or request_body.get("condition")
        )
        filter_match_type = str(filter_spec.get("matchType") or request_body.get("matchType") or "PARTIAL")
        if not isinstance(filter_expression, str) or not filter_expression.strip():
            raise ValueError(f"Control '{control_id}' is FILTER_CONTROL but has no valid filterSpec.filterExpression")
        normalized_filter_expression = _normalize_filter_expression(filter_expression)
        payload["rule"] = {"filter_action": {"filter": normalized_filter_expression}}

        if filter_query_condition is not None:
            if not isinstance(filter_query_condition, str) or not filter_query_condition.strip():
                raise ValueError(
                    f"Control '{control_id}' is FILTER_CONTROL but has invalid queryCondition"
                )
            payload["rule"]["condition"] = _build_query_condition(
                query_condition=filter_query_condition,
                match_type=filter_match_type,
                control_id=control_id,
            )
            return payload

        return _ensure_rule_condition(payload)

    if control_type in {"BOOST_CONTROL", "BURY_CONTROL"}:
        boost_spec = request_body.get("boostSpec", {})
        if control_type == "BURY_CONTROL":
            bury_spec = request_body.get("burySpec", {})
            if isinstance(bury_spec, dict) and bury_spec:
                boost_spec = bury_spec

        products_filter = boost_spec.get("condition")
        query_condition = boost_spec.get("queryCondition")
        match_type = str(boost_spec.get("matchType") or "FULL")
        boost_strength = boost_spec.get("boostStrength")

        if control_type == "BURY_CONTROL" and boost_strength is None:
            boost_strength = boost_spec.get("buryStrength")

        if not isinstance(products_filter, str) or not products_filter.strip():
            raise ValueError(
                f"Control '{control_id}' is {control_type} but has no valid condition in spec"
            )
        if not isinstance(boost_strength, (float, int)):
            raise ValueError(
                f"Control '{control_id}' is {control_type} but has no valid boostStrength/buryStrength"
            )

        if control_type == "BURY_CONTROL":
            boost_strength = -abs(float(boost_strength))
        else:
            boost_strength = float(boost_strength)

        normalized_products_filter = _normalize_filter_expression(products_filter)
        payload["rule"] = {
            "boost_action": {
                "products_filter": normalized_products_filter,
                "boost": boost_strength,
            }
        }

        if query_condition is not None:
            if not isinstance(query_condition, str) or not query_condition.strip():
                raise ValueError(
                    f"Control '{control_id}' is {control_type} but has invalid queryCondition"
                )
            payload["rule"]["condition"] = _build_query_condition(
                query_condition=query_condition,
                match_type=match_type,
                control_id=control_id,
            )
            return payload

        return _ensure_rule_condition(payload)

    if control_type == "REDIRECT_CONTROL":
        redirect_spec = request_body.get("redirectSpec", {})
        redirect_uri = redirect_spec.get("redirectUri")
        redirect_query_condition = (
            redirect_spec.get("queryCondition")
            or redirect_spec.get("condition")
            or request_body.get("queryCondition")
            or request_body.get("condition")
        )
        redirect_match_type = str(redirect_spec.get("matchType") or request_body.get("matchType") or "PARTIAL")
        if not isinstance(redirect_uri, str) or not redirect_uri.strip():
            raise ValueError(f"Control '{control_id}' is REDIRECT_CONTROL but has no valid redirectSpec.redirectUri")
        payload["rule"] = {"redirect_action": {"redirect_uri": redirect_uri}}

        if redirect_query_condition is not None:
            if not isinstance(redirect_query_condition, str) or not redirect_query_condition.strip():
                raise ValueError(
                    f"Control '{control_id}' is REDIRECT_CONTROL but has invalid queryCondition"
                )
            payload["rule"]["condition"] = _build_query_condition(
                query_condition=redirect_query_condition,
                match_type=redirect_match_type,
                control_id=control_id,
            )
            return payload

        return _ensure_rule_condition(payload)

    if control_type == "SYNONYM_CONTROL":
        synonym_spec = request_body.get("synonymSpec", {})
        synonyms = synonym_spec.get("synonyms", [])
        if len(synonyms) != 1 or not isinstance(synonyms[0], dict):
            raise ValueError(f"Control '{control_id}' is SYNONYM_CONTROL but has no valid synonymSpec.synonyms terms")

        terms = synonyms[0].get("terms", [])
        if not isinstance(terms, list):
            raise ValueError(f"Control '{control_id}' is SYNONYM_CONTROL but has no valid synonymSpec.synonyms terms")

        deduped_terms = list(dict.fromkeys(
            term.strip() for term in terms if isinstance(term, str) and term.strip()
        ))
        if len(deduped_terms) < 2:
            raise ValueError(
                f"Control '{control_id}' is SYNONYM_CONTROL but each synonym group must contain at least 2 distinct terms"
            )

        payload["rule"] = {"twoway_synonyms_action": {"synonyms": deduped_terms}}
        return _ensure_rule_condition(payload)

    if control_type == "PRODUCT_PINNING":
        pin_spec = request_body.get("productPinningSpec", {})
        pin_query_condition = (
            pin_spec.get("queryCondition")
            or pin_spec.get("condition")
            or request_body.get("queryCondition")
            or request_body.get("condition")
        )
        pin_match_type = str(pin_spec.get("matchType") or request_body.get("matchType") or "PARTIAL")
        pinned_products = pin_spec.get("pinnedProducts", [])
        if not isinstance(pinned_products, list):
            raise ValueError(f"Control '{control_id}' is PRODUCT_PINNING but has no valid productPinningSpec.pinnedProducts")
        if not pinned_products:
            raise ValueError(f"Control '{control_id}' is PRODUCT_PINNING but pinnedProducts is empty")

        if not isinstance(pin_query_condition, str) or not pin_query_condition.strip():
            raise ValueError(
                f"Control '{control_id}' is PRODUCT_PINNING but must include productPinningSpec.queryCondition "
                "(or legacy condition), for example 'query:sugar'."
            )

        pin_map = _normalize_pinned_products(
            control_id=control_id,
            pinned_products=pinned_products,
            product_service_client=product_service_client,
        )

        # Retail pin action is map<int_position, product_id>.
        payload["rule"] = {
            "pin_action": {"pin_map": pin_map},
            "condition": _build_query_condition(
                query_condition=pin_query_condition,
                match_type=pin_match_type,
                control_id=control_id,
            ),
        }
        return payload

    if control_type == "FACET_CONTROL":
        facet_spec = request_body.get("facetSpec", {})
        included = facet_spec.get("includedAttributes", [])
        excluded = facet_spec.get("excludedAttributes", [])

        clean_included = [
            _normalize_facet_attribute_name(a)
            for a in included
            if isinstance(a, str) and a.strip()
        ]
        clean_excluded = [
            _normalize_facet_attribute_name(a)
            for a in excluded
            if isinstance(a, str) and a.strip()
        ]

        if clean_included:
            payload["rule"] = {
                "force_return_facet_action": {
                    "facet_position_adjustments": [
                        {"attribute_name": name, "position": idx + 1}
                        for idx, name in enumerate(clean_included)
                    ]
                }
            }
            return _ensure_rule_condition(payload)

        if clean_excluded:
            payload["rule"] = {
                "remove_facet_action": {
                    "attribute_names": clean_excluded
                }
            }
            return _ensure_rule_condition(payload)

        raise ValueError(f"Control '{control_id}' is FACET_CONTROL but includes no valid includedAttributes/excludedAttributes")

    # Retail API does not support these request-body types as Control actions.
    if control_type in {"RELEVANCE_CONTROL", "QUERY_EXPANSION_CONTROL"}:
        print(f"Skipping unsupported control type '{control_type}' for '{control_id}' in Retail API")
        return None

    raise ValueError(f"Unsupported control type '{control_type}' for control '{control_id}'")


def load_controls_from_json(file_path: str) -> dict[str, dict[str, Any]]:
    path = Path(file_path)
    if not path.exists():
        print(f"Controls file not found at {path}. No controls will be loaded.")
        return {}

    with path.open("r", encoding="utf-8") as fp:
        data = json.load(fp)

    controls = data.get("controls")
    if controls is None:
        raise ValueError("Invalid controls JSON: expected top-level key 'controls'.")

    validated_controls: dict[str, dict[str, Any]] = {}

    # New format: controls is an array of full Control request bodies.
    if isinstance(controls, list):
        for item in controls:
            if not isinstance(item, dict):
                raise ValueError("Invalid controls JSON: every item in 'controls' must be an object.")

            control_id = item.get("controlId")
            if not isinstance(control_id, str) or not control_id.strip():
                raise ValueError("Invalid control item: 'controlId' must be a non-empty string.")

            if str(item.get("type") or "").upper() == "SYNONYM_CONTROL":
                validated_controls.update(_build_synonym_group_controls(control_id, item))
                continue

            request_body = {k: v for k, v in item.items() if k != "controlId"}
            if not request_body:
                raise ValueError(f"Invalid control '{control_id}': request body cannot be empty.")

            validated_controls[control_id] = {
                "type": item.get("type"),
                "request_body": request_body,
            }

        return validated_controls

    # Legacy format: controls is an object keyed by control id.
    if isinstance(controls, dict):
        for control_id, control_data in controls.items():
            if not isinstance(control_data, dict):
                raise ValueError(f"Invalid control '{control_id}': expected object value.")

            attribute = control_data.get("attribute")
            condition = control_data.get("condition")
            entities = control_data.get("entities")

            if not isinstance(attribute, str) or not attribute.strip():
                raise ValueError(f"Invalid control '{control_id}': 'attribute' must be a non-empty string.")

            if not isinstance(condition, str) or not condition.strip():
                raise ValueError(f"Invalid control '{control_id}': 'condition' must be a non-empty string.")

            if not isinstance(entities, list) or not entities or not all(isinstance(e, str) and e.strip() for e in entities):
                raise ValueError(
                    f"Invalid control '{control_id}': 'entities' must be a non-empty array of non-empty strings."
                )

            validated_controls[control_id] = {
                "type": "FILTER_CONTROL",
                "request_body": {
                    "rule": {
                        "condition": {"query_terms": []},
                        "filter_action": {
                            "filter": _build_legacy_filter_condition(control_data)
                        },
                    }
                },
            }

        return validated_controls

    raise ValueError("Invalid controls JSON: 'controls' must be either an object or an array.")




default_controls_file = Path(__file__).resolve().parent / "Controls" / "add_controls.json"
controls_file = environ.get("CONTROL_DEFINITIONS_FILE") or str(default_controls_file)
control_definitions = load_controls_from_json(controls_file)

if not control_definitions:
    raise ValueError(
        f"No controls found in {controls_file}. Add controls under the top-level 'controls' key."
    )

print(f"Loaded {len(control_definitions)} control(s) from {controls_file}")

control_service_client = ControlServiceClient()
serving_config_client = ServingConfigServiceClient()
product_service_client = ProductServiceClient()

full_serving_config_path = f"{parent}/servingConfigs/{agent_config_id}"

try:
    serving_config = serving_config_client.get_serving_config(name=full_serving_config_path)
    print(f"Found existing config. Attaching requested controls.")
except NotFound:
    serving_config = ServingConfig(
        display_name=agent_config_id,
        solution_types=[SolutionType.SOLUTION_TYPE_SEARCH],
    )
    serving_config_request = CreateServingConfigRequest(
        parent=parent,
        serving_config=serving_config,
        serving_config_id=agent_config_id,
    )
    serving_config_client.create_serving_config(request=serving_config_request)
    print(f"Serving Config with id: {agent_config_id} created")

for control_id, control_spec in control_definitions.items():
    control_payload = _build_control_payload_from_request(
        control_id,
        control_spec,
        product_service_client,
    )
    if control_payload is None:
        continue

    control = Control(control_payload)

    if not control.display_name:
        control.display_name = control_id

    if not control.solution_types:
        control.solution_types = [SolutionType.SOLUTION_TYPE_SEARCH]

    control_request = CreateControlRequest(
        parent=parent,
        control=control,
        control_id=control_id,
    )
    full_control_path = f"{parent}/controls/{control_id}"

    try:
        control_service_client.create_control(request=control_request)
        print(f"Control with id: {control_id} created")
    except AlreadyExists:
        was_updated = _update_existing_control(
            control_service_client=control_service_client,
            control_name=full_control_path,
            desired_control=control,
        )
        if was_updated:
            print(f"Control with id: {control_id} updated")
        else:
            print(f"Control with id: {control_id} already exists")

    try:
        add_control_request = serving_config_service.AddControlRequest(
            serving_config=full_serving_config_path,
            control_id=control_id,
        )
        serving_config_client.add_control(request=add_control_request)
        print(f"Control with id: {control_id} attached to serving config")
    except AlreadyExists:
        print(f"Control with id: {control_id} already attached to serving config")

