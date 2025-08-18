#!/bin/bash
set -e

for dir in samples/nodejs/*/; do
    if [ -d "$dir" ]; then
        echo "Updating $dir"
        (cd "$dir" && rm -rf node_modules/ package-lock.json && npm install)
    fi
done