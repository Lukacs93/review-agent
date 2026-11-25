#!/usr/bin/env bash
# Builds review-agent and starts the MCP server and the API in the background, as the AppHost does locally, then
# waits until both answer their health checks. Every setting comes from environment variables
# (PullRequests__Source, Agent__ApiKey, Storage__DataDirectory and the others the workflow sets).
#
# Usage: start-services.sh <review-agent checkout> <log directory>
set -euo pipefail

repository_root="$1"
log_directory="$2"

# The addresses in the projects' launch settings; the API's default Agent:McpServerUrl points to the MCP server's.
mcp_server_url="http://localhost:5090"
api_url="http://localhost:5080"

# Both start in a few seconds once built; 60 tries two seconds apart leave room for a slow runner.
health_check_tries=60
health_check_delay_seconds=2

mkdir -p "$log_directory"
dotnet build "$repository_root/ReviewAgent.slnx" --configuration Release

start() {
  local project="$1" url="$2" log_file="$3"
  nohup dotnet run --no-build --configuration Release --project "$repository_root/src/$project" --urls "$url" \
    > "$log_directory/$log_file" 2>&1 &
}

wait_until_healthy() {
  local url="$1"
  curl --silent --show-error --fail --retry "$health_check_tries" --retry-delay "$health_check_delay_seconds" \
    --retry-all-errors "$url/health" > /dev/null
}

start ReviewAgent.McpServer "$mcp_server_url" mcp-server.log
start ReviewAgent.Api "$api_url" api.log

wait_until_healthy "$mcp_server_url"
wait_until_healthy "$api_url"
echo "The MCP server and the API are running."
