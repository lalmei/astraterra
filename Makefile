SHELL := /bin/zsh

DOTNET_ROOT := $(CURDIR)/.dotnet
DOTNET_CLI_HOME := $(CURDIR)/.dotnet-home
DOTNET_ENV := PATH="$(DOTNET_ROOT):$$PATH" DOTNET_CLI_HOME="$(DOTNET_CLI_HOME)"
DOTNET := $(DOTNET_ENV) dotnet

CONFIGURATION ?= Release
TARGET_FRAMEWORK := net10.0
GAME_APP ?= /Applications/Vintage Story.app
MODS_DIR ?= $(HOME)/Library/Application Support/VintagestoryData/Mods
DEPLOY_DIR := $(MODS_DIR)/AstraTerra
BUILD_OUTPUT_DIR := src/AstraTerra/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)
DIST_DIR := dist
MOD_VERSION = $(shell perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json)
PACKAGE_FILE = $(DIST_DIR)/AstraTerra-$(MOD_VERSION).zip

.PHONY: help test build package deploy run deploy-run docs-build docs-serve moddb-preview moddb-copy bump-version bump-minor-version bump-patch-version bump-version-files

help:
	@printf "Targets:\n"
	@printf "  make test        Run the test suite\n"
	@printf "  make build       Build the mod in $(CONFIGURATION)\n"
	@printf "  make package     Build and zip the mod into $(DIST_DIR)/\n"
	@printf "  make deploy      Build and install into Vintage Story Mods\n"
	@printf "  make run         Launch Vintage Story.app\n"
	@printf "  make deploy-run  Deploy the mod, then launch the game\n"
	@printf "  make docs-build  Build the documentation site\n"
	@printf "  make docs-serve  Serve the documentation site locally\n"
	@printf "  make moddb-preview  Render the ModDB description locally and open it\n"
	@printf "  make moddb-copy     Copy the paste-ready ModDB description to the clipboard\n"
	@printf "  make bump-version VERSION=0.1.2  Update, build, and deploy mod version\n"
	@printf "  make bump-minor-version  Increment minor version, reset patch to 0, build, and deploy\n"
	@printf "  make bump-patch-version  Increment patch version, build, and deploy\n"

test:
	@env $(DOTNET_ENV) dotnet test tests/AstraTerra.Tests/AstraTerra.Tests.csproj -c $(CONFIGURATION) -v minimal

build:
	@env $(DOTNET_ENV) dotnet build src/AstraTerra/AstraTerra.csproj -c $(CONFIGURATION) -v minimal

package: build
	@mkdir -p "$(DIST_DIR)"
	@rm -f "$(PACKAGE_FILE)"
	@cd "$(BUILD_OUTPUT_DIR)" && zip -qr "$(CURDIR)/$(PACKAGE_FILE)" .
	@printf "Packaged $(PACKAGE_FILE)\n"

deploy: build
	@rm -rf "$(DEPLOY_DIR)"
	@mkdir -p "$(MODS_DIR)"
	@cp -R "$(BUILD_OUTPUT_DIR)" "$(DEPLOY_DIR)"

run:
	@open -a "$(GAME_APP)"

deploy-run: deploy run

docs-build docs-serve: SHELL := /bin/sh

docs-build:
	@uv run --with-requirements docs/requirements.txt properdocs build -f properdocs.yml --strict

docs-serve:
	@uv run --with-requirements docs/requirements.txt properdocs serve -f properdocs.yml

MODDB_SOURCE := docs/moddb-description.html
MODDB_PREVIEW := $(DIST_DIR)/moddb-preview.html

$(MODDB_PREVIEW): $(MODDB_SOURCE) tools/moddb_preview.py
	@python3 tools/moddb_preview.py --out "$(MODDB_PREVIEW)" >/dev/null

moddb-preview: $(MODDB_PREVIEW)
	@open "$(MODDB_PREVIEW)"

moddb-copy:
	@python3 tools/moddb_preview.py --paste | pbcopy
	@printf "Paste-ready ModDB description copied to the clipboard\n"

bump-version: bump-version-files deploy

bump-version-files:
	@if [[ -z "$(VERSION)" ]]; then printf "Usage: make bump-version VERSION=0.1.2\n"; exit 2; fi
	@if ! [[ "$(VERSION)" =~ ^[0-9]+\.[0-9]+\.[0-9]+$$ ]]; then printf "VERSION must look like 0.1.2\n"; exit 2; fi
	@perl -0pi -e 's/"version":\s*"[^"]+"/"version": "$(VERSION)"/' modinfo.json
	@perl -0pi -e 's/public const string Version = "[^"]+";/public const string Version = "$(VERSION)";/' src/AstraTerra/AstraTerraModMetadata.cs
	@for f in .github/ISSUE_TEMPLATE/*.yml; do \
		perl -0pi -e 's/(id: mod-version.*?placeholder:\s*)v?[0-9]+\.[0-9]+\.[0-9]+/$${1}v$(VERSION)/s' "$$f"; \
	done
	@printf "Bumped AstraTerra source version to $(VERSION)\n"

bump-minor-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$(( $$parts[2] + 1 )).0"; \
	$(MAKE) bump-version VERSION=$$new_version

bump-patch-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$parts[2].$$(( $$parts[3] + 1 ))"; \
	$(MAKE) bump-version VERSION=$$new_version
