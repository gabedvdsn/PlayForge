#!/bin/bash
# Builds the documentation site from what's committed and publishes it to the public PlayForge-Docs repository, whose
# GitHub Pages serves it at https://gabedvdsn.github.io/PlayForge-Docs/. The guides come from Assets/PlayForge/Documentation
# and the API reference from the package's source, so publishing after a commit that changes them is all it takes. The
# build reads a copy of HEAD, so changes not yet committed never reach the site. Run from anywhere: bash docfx/publish.sh
set -e
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
dotnet tool restore > /dev/null

head="$(git rev-parse --short HEAD)"
source_copy="$(mktemp -d)"
git archive HEAD Assets/PlayForge/Runtime Assets/PlayForge/Editor Assets/PlayForge/TestKit \
  Assets/PlayForge/Documentation docfx .config ProjectSettings/ProjectVersion.txt | tar -x -C "$source_copy"
PLAYFORGE_LIBRARY="$repo_root/Library" python "$source_copy/docfx/build.py"

work="$(mktemp -d)"
git clone --quiet --depth 1 https://github.com/gabedvdsn/PlayForge-Docs.git "$work" 2> /dev/null
git -C "$work" checkout --quiet -B main
find "$work" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r "$source_copy/docfx/_site/." "$work/"
# GitHub Pages serves the files as they are, folders starting with _ included
touch "$work/.nojekyll"

git -C "$work" add -A
if git -C "$work" diff --cached --quiet; then
  echo "The site is unchanged."
else
  git -C "$work" commit --quiet -m "Site from PlayForge $head"
  git -C "$work" push --quiet origin main
  echo "Published: https://gabedvdsn.github.io/PlayForge-Docs/"
fi
rm -rf "$work" "$source_copy"
