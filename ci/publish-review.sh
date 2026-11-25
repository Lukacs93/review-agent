#!/usr/bin/env bash
# Publishes a finished review with the running API: approves every comment the verifier kept, asks for publishing,
# and waits until the review is on the pull request. The person who approved the workflow's environment made the
# decision; the API records the approvals as for any developer.
#
# Usage: publish-review.sh <review run ID>
set -euo pipefail

run_id="$1"

api_url="http://localhost:5080"

# Publishing is one request to GitHub; a check every few seconds is enough.
poll_interval_seconds=5

review=$(curl --silent --show-error --fail-with-body "$api_url/api/reviews/$run_id")
comment_ids=$(jq --raw-output '.comments[] | select(.rejectionReason == null and .decision == "Pending") | .id' \
  <<< "$review")

for comment_id in $comment_ids; do
  curl --silent --show-error --fail-with-body --request POST \
    "$api_url/api/reviews/$run_id/comments/$comment_id/approve" > /dev/null
done

curl --silent --show-error --fail-with-body --request POST "$api_url/api/reviews/$run_id/publish" > /dev/null

while true; do
  review=$(curl --silent --show-error --fail-with-body "$api_url/api/reviews/$run_id")
  status=$(jq --raw-output .status <<< "$review")
  [[ "$status" != "Publishing" ]] && break
  sleep "$poll_interval_seconds"
done

if [[ "$status" != "Published" ]]; then
  echo "Publishing failed: $(jq --raw-output '.publishingFailureReason // "no reason given"' <<< "$review")" >&2
  exit 1
fi

echo "## The review is published on the pull request." >> "$GITHUB_STEP_SUMMARY"
