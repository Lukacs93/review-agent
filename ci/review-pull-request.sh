#!/usr/bin/env bash
# Reviews one pull request with the running API, waits until the agents finish, and writes the result to the job
# summary. Saves the review and its run log as JSON into the output directory. Writes the run's ID and status to the
# step's outputs. Fails when the review fails.
#
# Usage: review-pull-request.sh <owner> <repository> <pull request number> <output directory>
set -euo pipefail

owner="$1"
repository="$2"
pull_request_number="$3"
output_directory="$4"

api_url="http://localhost:5080"

# The review's status changes once, when the agents finish; a check every few seconds is enough. The agent's own
# time limit (Agent:Timeout) ends a review that hangs, so the loop needs no limit of its own.
poll_interval_seconds=5

mkdir -p "$output_directory"

request=$(jq --null-input --arg owner "$owner" --arg repository "$repository" --argjson number "$pull_request_number" \
  '{repositoryOwner: $owner, repositoryName: $repository, pullRequestNumber: $number}')
run_id=$(curl --silent --show-error --fail-with-body --json "$request" "$api_url/api/reviews" | jq --raw-output .id)
echo "Review run $run_id started."

while true; do
  review=$(curl --silent --show-error --fail-with-body "$api_url/api/reviews/$run_id")
  status=$(jq --raw-output .status <<< "$review")
  [[ "$status" != "Running" ]] && break
  sleep "$poll_interval_seconds"
done

echo "$review" > "$output_directory/review.json"
curl --silent --show-error --fail-with-body "$api_url/api/reviews/$run_id/log" > "$output_directory/run-log.json"

# The code goes into <pre> blocks, HTML-escaped: a Markdown code fence could be ended early by backticks in the code.
jq --raw-output '
  def kept: [.comments[] | select(.rejectionReason == null)];
  def rejected: [.comments[] | select(.rejectionReason != null)];
  def count(value): if value == null then "not reported" else (value | tostring) end;
  "## Review of pull request #\(.pullRequestNumber)",
  "",
  "**Status:** \(.status)",
  "",
  (.summary // .failureReason // ""),
  "",
  "| Measure | Value |",
  "| --- | --- |",
  "| Model calls | \(.modelCallCount) |",
  "| Model time | \(.modelTime) |",
  "| Input tokens | \(count(.inputTokenCount)) |",
  "| Output tokens | \(count(.outputTokenCount)) |",
  "| Tool calls | \(.toolCallCount) |",
  "| Verifier | \(.verifierSummary // "did not run") |",
  "",
  "### Comments to publish (\(kept | length))",
  "",
  (kept[] |
    "**\(.ruleId)** (\(.severity)) `\(.filePath):\(.line)`",
    "",
    .body,
    "",
    (if .replacementCode != null then "<pre>\(.replacementCode | @html)</pre>", "" else empty end)),
  (if (rejected | length) > 0 then
    "<details><summary>Rejected by the verifier (\(rejected | length))</summary>",
    "",
    (rejected[] | "- **\(.ruleId)** `\(.filePath):\(.line)`: \(.rejectionReason)"),
    "",
    "</details>"
  else empty end)
' "$output_directory/review.json" >> "$GITHUB_STEP_SUMMARY"

{
  echo "run-id=$run_id"
  echo "status=$status"
} >> "$GITHUB_OUTPUT"

if [[ "$status" != "AwaitingApproval" ]]; then
  echo "The review ended with the status $status." >&2
  exit 1
fi
