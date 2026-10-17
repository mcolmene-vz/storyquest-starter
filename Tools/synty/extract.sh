#!/bin/bash
# Extracts Synty .unitypackage files without importing them (ARCHITECTURE §15.1, §15.15.6).
#
#   Tools/synty/extract.sh <package.unitypackage>...
#
# Each package must be a row of Tools/synty/packs.txt (matched by file name, checked by SHA-256) and is unpacked
# with tar into a fresh Builds/synty-extract/<stem>/. Then, across every pack in packs.txt:
#   - Tools/synty/package-contents.txt is rewritten: every pathname, grouped by pack;
#   - every Tools/synty/keep.txt line is copied, `asset` only, to Assets/ThirdParty/Synty/<subfolder>/<basename>.
#     Synty's asset.meta is never copied: Unity writes fresh .meta files and SyntyPostprocessor sets every rule.
#   - any file under Assets/ThirdParty/Synty/ that is not a keep.txt line fails the run.
# Identical files are left untouched, so a second run changes nothing.
# Nothing inside a package is executed; pathname files are read as text and used only for matching.
#
# keep.txt: `# pack: <store_name>` starts a group (store_name as in packs.txt); each line after it is
# `<package pathname> -> <Characters|Weapons|Textures|Animations>`; other `#` lines and blank lines are comments.
set -euo pipefail
export LC_ALL=C

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
packs="$here/packs.txt"
keep="$here/keep.txt"
contents="$here/package-contents.txt"
scratch="$repo/Builds/synty-extract"
dest="$repo/Assets/ThirdParty/Synty"
subfolders="Characters Weapons Textures Animations"
tab="$(printf '\t')"

die() { echo "extract.sh: $*" >&2; exit 1; }

# field <column> <match column> <value>: a packs.txt field from the row whose match column equals value.
# Columns: 1 store_name, 2 version, 3 downloaded, 4 licence, 5 licence_date, 6 sha256, 7 file.
field() { awk -F'\t' -v c="$1" -v m="$2" -v v="$3" 'NR > 1 && $m == v { print $c; exit }' "$packs"; }

[ $# -ge 1 ] || die "usage: extract.sh <package.unitypackage>..."
[ -f "$packs" ] || die "$packs not found"
[ -f "$keep" ] || die "$keep not found"
mkdir -p "$scratch"

for pkg in "$@"; do
  [ -f "$pkg" ] || die "$pkg: no such file"
  file="$(basename "$pkg")"
  [ -n "$(field 1 7 "$file")" ] || die "$file is not in packs.txt; record the pack there before extracting it"
  want="$(field 6 7 "$file")"
  got="$(shasum -a 256 "$pkg" | awk '{ print $1 }')"
  [ "$got" = "$want" ] || die "$file: sha256 $got, packs.txt says $want"

  # A .unitypackage holds only <guid>/ folders of asset, asset.meta, pathname and preview.png. Anything else
  # (links, absolute or .. paths, other names) means it is not one, and nothing is unpacked.
  stem="${file%.unitypackage}"
  listing="$scratch/$stem.listing"
  tar -tvzf "$pkg" | awk '{ t = substr($1, 1, 1); if (t != "-" && t != "d") print }' > "$listing"
  [ ! -s "$listing" ] || die "$file: entries that are not plain files or folders, e.g. $(head -n 1 "$listing")"
  tar -tzf "$pkg" | { grep -Ev '^(\./)?[0-9a-f]{32}/(asset|asset\.meta|pathname|preview\.png)?$' || true; } > "$listing"
  [ ! -s "$listing" ] || die "$file: unexpected entries, e.g. $(head -n 1 "$listing")"
  rm -f "$listing"

  out="$scratch/$stem"
  rm -rf "$out"
  mkdir -p "$out"
  tar -xzf "$pkg" -C "$out" --no-same-owner
  # <guid> TAB <pathname>, sorted by pathname: the first line of each pathname file (some end in a second "00" line).
  find "$out" -mindepth 2 -maxdepth 2 -type f -name pathname -print0 \
    | xargs -0 awk 'FNR == 1 { n = split(FILENAME, p, "/"); sub(/\r$/, ""); print p[n - 1] "\t" $0 }' \
    | sort -t "$tab" -k 2 > "$scratch/$stem.index"
  echo "extracted $file: $(wc -l < "$scratch/$stem.index" | tr -d ' ') pathnames -> ${out#$repo/}/"
done

# Everything below spans every pack in packs.txt, so the outputs never depend on which packages were passed.
pack_rows="$(awk -F'\t' 'NR > 1 && $1 != "" { print $1 }' "$packs")"

tmp_contents="$scratch/package-contents.txt.new"
{
  echo "# Every pathname in each Synty package in packs.txt, written by Tools/synty/extract.sh; do not edit."
  echo "# Grouped by pack in packs.txt order, sorted within each pack. The diff base for a future Synty update."
  while IFS= read -r name; do
    file="$(field 7 1 "$name")"
    index="$scratch/${file%.unitypackage}.index"
    [ -f "$index" ] || die "$file has not been extracted; pass it to extract.sh"
    echo
    echo "## $name $(field 2 1 "$name") ($file)"
    cut -f 2 "$index"
  done <<< "$pack_rows"
} > "$tmp_contents"
if cmp -s "$tmp_contents" "$contents"; then rm -f "$tmp_contents"; else mv "$tmp_contents" "$contents"; echo "wrote ${contents#$repo/}"; fi

kept="$scratch/kept.txt"
: > "$kept"
pack=""
copied=0
unchanged=0
lineno=0
while IFS= read -r line || [ -n "$line" ]; do
  lineno=$((lineno + 1))
  case "$line" in
    "# pack: "*)
      pack="${line#\# pack: }"
      [ -n "$(field 1 1 "$pack")" ] || die "keep.txt:$lineno: pack '$pack' is not in packs.txt"
      continue ;;
    "" | "#"*) continue ;;
    *" -> "*) ;;
    *) die "keep.txt:$lineno: expected '<package pathname> -> <subfolder>'" ;;
  esac
  [ -n "$pack" ] || die "keep.txt:$lineno: line before any '# pack:' header"
  path="${line% -> *}"
  sub="${line##* -> }"
  case " $subfolders " in *" $sub "*) ;; *) die "keep.txt:$lineno: subfolder '$sub' is not one of: $subfolders" ;; esac

  file="$(field 7 1 "$pack")"
  stem="${file%.unitypackage}"
  guids="$(awk -F'\t' -v p="$path" '$2 == p { print $1 }' "$scratch/$stem.index")"
  case "$(printf '%s' "$guids" | grep -c .)" in
    1) ;;
    0) die "keep.txt:$lineno: '$path' is not in $file" ;;
    *) die "keep.txt:$lineno: '$path' appears more than once in $file" ;;
  esac
  src="$scratch/$stem/$guids/asset"
  [ -f "$src" ] && [ ! -L "$src" ] || die "keep.txt:$lineno: '$path' has no asset file (a folder?)"

  target="$dest/$sub/$(basename "$path")"
  ! grep -Fxq "$target" "$kept" || die "keep.txt:$lineno: a second line copies to ${target#$repo/}"
  echo "$target" >> "$kept"
  mkdir -p "$dest/$sub"
  if [ -f "$target" ] && cmp -s "$src" "$target"; then
    unchanged=$((unchanged + 1))
  else
    cp "$src" "$target"
    chmod 0644 "$target"
    copied=$((copied + 1))
    echo "copied ${target#$repo/}"
  fi
done < "$keep"

stray=0
if [ -d "$dest" ]; then
  while IFS= read -r f; do
    grep -Fxq "$f" "$kept" || { echo "extract.sh: not on keep.txt: ${f#$repo/}" >&2; stray=$((stray + 1)); }
  done < <(find "$dest" -type f ! -name '*.meta' ! -name '.DS_Store' | sort)
fi

echo "keep.txt: $(wc -l < "$kept" | tr -d ' ') files ($copied copied, $unchanged unchanged) in ${dest#$repo/}/"
[ "$stray" -eq 0 ] || die "$stray file(s) under ${dest#$repo/}/ are not keep.txt lines; remove them or add them to keep.txt"
