#!/bin/bash
# Builds the documentation site and publishes it to the public PlayForge-Docs repository, whose GitHub Pages serves it at
# https://gabedvdsn.github.io/PlayForge-Docs/. The guides come from Assets/PlayForge/Documentation, the pages the
# package ships, so publishing after a change to them is all it takes. Run from anywhere: bash docfx/publish.sh
set -e
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
dotnet tool restore > /dev/null
dotnet docfx docfx/docfx.json

work="$(mktemp -d)"
git clone --quiet --depth 1 https://github.com/gabedvdsn/PlayForge-Docs.git "$work" 2> /dev/null
git -C "$work" checkout --quiet -B main
find "$work" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r docfx/_site/. "$work/"
# GitHub Pages serves the files as they are, folders starting with _ included
touch "$work/.nojekyll"

git -C "$work" add -A
if git -C "$work" diff --cached --quiet; then
  echo "The site is unchanged."
else
  git -C "$work" commit --quiet -m "Site from PlayForge $(git rev-parse --short HEAD)"
  git -C "$work" push --quiet origin main
  echo "Published: https://gabedvdsn.github.io/PlayForge-Docs/"
fi
rm -rf "$work"
