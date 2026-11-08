#!/usr/bin/env bash
# Copies every photo from the old Wix site into wwwroot/images so the new site
# no longer depends on Wix. Run once from the RachelTower.Web folder:
#   bash scripts/download-images.sh
# The list of photos comes from Infrastructure/SiteImages.cs.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
target="$root/wwwroot/images"
mkdir -p "$target"

grep -oE 'new\("[a-z0-9-]+", *"[0-9a-f]{32}"(, *"[a-z]+")?' "$root/Infrastructure/SiteImages.cs" |
while IFS= read -r line; do
  key=$(sed -E 's/new\("([^"]+)".*/\1/' <<< "$line")
  id=$(sed -E 's/.*"([0-9a-f]{32})".*/\1/' <<< "$line")
  ext=$(grep -oE ', *"[a-z]+"$' <<< "$line" | tr -dc 'a-z' || true)
  ext=${ext:-jpg}
  file="$target/$key.$ext"
  [[ -f "$file" ]] && continue
  echo "Downloading $key.$ext"
  curl -fsSL "https://static.wixstatic.com/media/f2e2c0_${id}~mv2.${ext}/v1/fit/w_2000,h_4000,q_85/image.${ext}" -o "$file"
done
echo "Done. Photos are in wwwroot/images."
