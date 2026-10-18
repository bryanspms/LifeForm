#!/bin/bash

PROJECT_DIR="/home/bryan-price/Documents/UnityProjects/LifeForm/"

cd "$PROJECT_DIR" || exit 1

git add .

if git diff --cached --quiet; then
    echo "No changes detected."
    exit 0
fi

git commit -m "Update $(date '+%Y-%m-%d %H:%M:%S')"
git push origin master

echo "GitHub updated."