#!/bin/bash
set -e

cd "$(dirname "$0")"
if [[ -f env.sh ]]; then
  source env.sh
fi

projectId="${1:-${PROJECT_ID}}"
attributesConfigPath="${2:-${ATTRIBUTES_CONFIG_PATH}}"

if [[ -z "$projectId" || -z "$attributesConfigPath" ]]; then
  echo "Project id and path to the attributes config file must be specified."
  exit 1
fi

curl -X PATCH -H "Authorization: Bearer $(gcloud auth print-access-token)" \
  -H "X-Goog-User-Project: $projectId" \
  -H "Content-Type: application/json" \
  "https://retail.googleapis.com/v2beta/projects/$projectId/locations/global/catalogs/default_catalog/attributesConfig" \
  -d @"$attributesConfigPath"
