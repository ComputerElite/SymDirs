#!/bin/bash

# Extract version from pubspec.yaml
version=$(git describe --tags $(git rev-list --tags --max-count=1))

# Replace <version> in PKGBUILD.template with the extracted version
sed "s/<version>/$version/" PKGBUILD.template > PKGBUILD