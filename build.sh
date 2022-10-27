#!/usr/bin/env bash
pkill dotnet
chmod +x dotnet-install.sh
./dotnet-install.sh
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
set -euo pipefail
$HOME/.dotnet/dotnet run --project "./build/build.csproj" --no-launch-profile -- "$@"



 