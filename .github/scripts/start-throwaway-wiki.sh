#!/usr/bin/env bash
# Starts a MediaWiki in a container for the smoke tests, seeds it with something to read, and prints the REST API root
# once it answers.
#
# The wiki lives on SQLite inside the container, so it is gone with the container and nothing outside it is touched.
# Anonymous editing is switched on and the per-IP edit limit switched off, since the write tests edit without an account.
# The official image ships neither the wikidiff2 extension the revision comparison endpoint insists on nor linting
# switched on, so the first is built from source and the second enabled, which lets the smoke tests cover those
# endpoints too.
#
# The only argument is the MediaWiki version to start, one of the tags pinned below; it defaults to the oldest.
#
# Under Git Bash on Windows, run with MSYS_NO_PATHCONV=1, or the container paths below are rewritten to Windows ones.
set -euo pipefail

# The tag is what a reader compares against MediaWiki's release notes; the digest is what actually runs.
case "${1:-1.43}" in
    1.43) image='mediawiki:1.43@sha256:db1c14d264c96835434761f015e8d5b097fcbe5f7e5087875102dcd3f56c2c1d' ;;
    1.46) image='mediawiki:1.46@sha256:237bbc4cc4d986d9bd0966292dab4611646e188bc20477109e7f87f74f06e259' ;;
    *) echo "Unknown MediaWiki version '$1'; the pinned ones are 1.43 and 1.46." >&2; exit 1 ;;
esac
# The release tag is what a reader compares against wikidiff2's history; the checksum is what the download is held to.
wikidiff2_version='1.14.2'
wikidiff2_sha256='4dce99c13b62116e45f3b3f89a0d715108df5bec33f98d9b0fed7f7fadfba2b9'
name='smoke-wiki'
port='8080'
server="http://localhost:${port}"

docker run --detach --name "${name}" --publish "${port}:80" "${image}" > /dev/null

# The image carries PHP's build tools but not libthai, which wikidiff2 needs for Thai word breaking. The extension is
# enabled here for the command line; the web server picks it up when it is reloaded at the end.
docker exec "${name}" sh -c '
    set -e
    apt-get update > /dev/null
    DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends libthai-dev > /dev/null
    cd /tmp
    curl --silent --location --output wikidiff2.tar.gz \
        "https://github.com/wikimedia/mediawiki-php-wikidiff2/archive/refs/tags/'"${wikidiff2_version}"'.tar.gz"
    echo "'"${wikidiff2_sha256}"'  wikidiff2.tar.gz" | sha256sum --check --quiet
    mkdir wikidiff2
    tar --extract --gzip --file wikidiff2.tar.gz --directory wikidiff2 --strip-components 1
    cd wikidiff2
    phpize > /dev/null 2>&1
    ./configure > /dev/null
    make > /dev/null
    make install > /dev/null
    docker-php-ext-enable wikidiff2
'

# Apache takes a moment to come up. Any HTTP answer will do: before installation the wiki serves a setup notice.
for _ in $(seq 1 30); do
    if curl --silent "${server}/" > /dev/null; then
        break
    fi
    sleep 1
done

# The image serves MediaWiki at the document root, so the script path is empty rather than the /w of a typical wiki.
docker exec "${name}" php maintenance/run.php install \
    --dbtype=sqlite \
    --dbpath=/var/www/data \
    --server="${server}" \
    --scriptpath= \
    --lang=en \
    --pass='SmokeTestsOnly!' \
    'Smoke wiki' 'Admin' > /dev/null

docker exec --interactive "${name}" sh -c 'cat >> LocalSettings.php' <<'EOF'

# Added for the smoke tests: anonymous editing, without the per-IP limit that would throttle a run, a file to read, and
# the lint endpoints, which answer nothing until Parsoid is told to lint.
$wgGroupPermissions['*']['edit'] = true;
$wgGroupPermissions['*']['createpage'] = true;
$wgRateLimits = [];
$wgEnableUploads = true;
$wgParsoidSettings['linting'] = true;
EOF

# Seed what the read tests look for: a file, a page that embeds it and links to another language, with enough
# revisions to spill over one page of history, a redirect to it, and a second page that a search for the first word
# of the title finds, for the tests that need a revision of another page. The revision text has to change each time,
# since an edit that changes nothing is dropped rather than stored.
docker exec "${name}" mkdir /tmp/import
printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==' \
    | base64 --decode \
    | docker exec --interactive "${name}" sh -c 'cat > /tmp/import/Smoke.png'
docker exec "${name}" php maintenance/run.php importImages /tmp/import --comment='Seeded for the smoke tests' > /dev/null

docker exec "${name}" sh -c '
    for revision in $(seq 1 21); do
        printf "Smoke page, revision %s.\n\n[[File:Smoke.png]]\n\n[[de:Rauchtest]]\n" "${revision}" \
            | php maintenance/run.php edit --summary="Revision ${revision}" "Smoke page" > /dev/null
    done
    echo "#REDIRECT [[Smoke page]]" | php maintenance/run.php edit --summary="Redirect" "Smoke redirect" > /dev/null
    echo "Smoke companion, a page of its own." | php maintenance/run.php edit --summary="Companion" "Smoke companion" > /dev/null
'

# The installer and the seeding ran as root; the web server, which runs as www-data, has to be able to write the
# database and the thumbnails.
docker exec "${name}" chown --recursive www-data:www-data /var/www/data images

# The web server loaded PHP before wikidiff2 existed; a graceful reload picks the extension up without dropping the
# container. It warns about a missing ServerName, nothing more.
docker exec "${name}" apache2ctl graceful 2> /dev/null

# The last two seeded revisions belong to the page, so comparing them proves the web server sees wikidiff2.
latest="$(curl --silent --fail "${server}/rest.php/v1/page/Smoke_page/bare" | grep --only-matching '"latest":{"id":[0-9]*' | grep --only-matching '[0-9]*$')"
curl --silent --fail "${server}/rest.php/v1/revision/$((latest - 1))/compare/${latest}" > /dev/null

echo "${server}/rest.php/v1/"
