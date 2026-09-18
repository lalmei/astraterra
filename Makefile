SHELL := /bin/zsh

DOTNET_ROOT := $(CURDIR)/.dotnet
DOTNET_CLI_HOME := $(CURDIR)/.dotnet-home
DOTNET_ENV := PATH="$(DOTNET_ROOT):$$PATH" DOTNET_CLI_HOME="$(DOTNET_CLI_HOME)"
DOTNET := $(DOTNET_ENV) dotnet

CONFIGURATION ?= Release
TEST_ARGS ?=
TARGET_FRAMEWORK := net10.0
GAME_APP ?= /Applications/Vintage Story.app
MODS_DIR ?= $(HOME)/Library/Application Support/VintagestoryData/Mods
DEPLOY_DIR := $(MODS_DIR)/AstraTerra
BUILD_OUTPUT_DIR := src/AstraTerra/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)
DIST_DIR := dist
MOD_VERSION = $(shell perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' modinfo.json)
PACKAGE_FILE = $(DIST_DIR)/AstraTerra-$(MOD_VERSION).zip
# Version files are listed in .bumpversion.toml. Install once:
#   uv tool install bump-my-version
BUMP ?= bump-my-version
PART ?= patch

.PHONY: help test build package bench deploy run deploy-run docs-build docs-serve moddb-preview moddb-copy pose-build pose-preview bump-version bump-minor-version bump-patch-version bump-version-files

help:
	@printf "Targets:\n"
	@printf "  make test        Run the test suite\n"
	@printf "  make build       Build the mod in $(CONFIGURATION)\n"
	@printf "  make package     Build and zip the mod into $(DIST_DIR)/\n"
	@printf "  make bench       Run the near-body light cache workload and check its counts\n"
	@printf "  make deploy      Package the mod and install the zip into Vintage Story Mods\n"
	@printf "  make run         Launch Vintage Story.app\n"
	@printf "  make deploy-run  Deploy the mod, then launch the game\n"
	@printf "  make docs-build  Build the documentation site\n"
	@printf "  make docs-serve  Serve the documentation site locally\n"
	@printf "  make moddb-preview  Render the ModDB description locally and open it\n"
	@printf "  make moddb-copy     Copy the paste-ready ModDB description to the clipboard\n"
	@printf "  make pose-build     Rebuild the seraph stargaze clips into the shape patch\n"
	@printf "  make pose-preview CLIP=stargaze  Draw a clip as a PNG contact sheet and open it\n"
	@printf "  make bump-version VERSION=0.1.2  Update, build, and deploy mod version\n"
	@printf "  make bump-minor-version  Increment minor version, reset patch to 0, build, and deploy\n"
	@printf "  make bump-patch-version  Increment patch version, build, and deploy\n"

# Portable shell: these run on Linux CI as well as locally, and use no zsh syntax.
test build package bench: SHELL := /bin/sh

test:
	@env $(DOTNET_ENV) dotnet test tests/AstraTerra.Tests/AstraTerra.Tests.csproj -c $(CONFIGURATION) -v minimal $(TEST_ARGS)

build:
	@env $(DOTNET_ENV) dotnet build src/AstraTerra/AstraTerra.csproj -c $(CONFIGURATION) -v minimal

# Fails on the counts, never on the clock: see benchmarks/NearBodyLightCacheBenchmark.
bench:
	@env $(DOTNET_ENV) dotnet run --project benchmarks/NearBodyLightCacheBenchmark -c $(CONFIGURATION) -v minimal

package: build
	@mkdir -p "$(DIST_DIR)"
	@rm -f "$(PACKAGE_FILE)"
	@cd "$(BUILD_OUTPUT_DIR)" && zip -qr "$(CURDIR)/$(PACKAGE_FILE)" .
	@printf "Packaged $(PACKAGE_FILE)\n"

deploy: package
	@mkdir -p "$(MODS_DIR)"
	@rm -rf "$(DEPLOY_DIR)"
	@rm -f "$(MODS_DIR)"/AstraTerra-*.zip(N)
	@cp "$(PACKAGE_FILE)" "$(MODS_DIR)/"
	@printf "Deployed $(PACKAGE_FILE) to $(MODS_DIR)/\n"

run:
	@open -a "$(GAME_APP)"

deploy-run: deploy run

docs-build docs-serve: SHELL := /bin/sh

docs-build:
	@uv run --with-requirements docs/requirements.txt properdocs build -f properdocs.yml --strict

docs-serve:
	@uv run --with-requirements docs/requirements.txt properdocs serve -f properdocs.yml

CLIP ?= stargaze
POSE_PREVIEW := $(DIST_DIR)/pose-preview.png

pose-build:
	@GAME_APP="$(GAME_APP)" python3 tools/build_stargaze_clips.py

pose-preview:
	@GAME_APP="$(GAME_APP)" python3 tools/preview_seraph_pose.py --clip "$(CLIP)" --tween 1 --out "$(POSE_PREVIEW)" --open

MODDB_SOURCE := docs/moddb-description.html
MODDB_PREVIEW := $(DIST_DIR)/moddb-preview.html

$(MODDB_PREVIEW): $(MODDB_SOURCE) tools/moddb_preview.py
	@python3 tools/moddb_preview.py --out "$(MODDB_PREVIEW)" >/dev/null

moddb-preview: $(MODDB_PREVIEW)
	@open "$(MODDB_PREVIEW)"

moddb-copy:
	@python3 tools/moddb_preview.py --paste | pbcopy
	@printf "Paste-ready ModDB description copied to the clipboard\n"

# Re-invoke make after rewriting the version so PACKAGE_FILE picks up the new number.
bump-version-files:
	@if [[ -n "$(VERSION)" ]]; then \
		if ! [[ "$(VERSION)" =~ ^[0-9]+\.[0-9]+\.[0-9]+$$ ]]; then printf "VERSION must look like 0.1.2\n"; exit 2; fi; \
		$(BUMP) bump --new-version "$(VERSION)"; \
	else \
		$(BUMP) bump $(PART); \
	fi
	@printf "Bumped AstraTerra source version to $$($(BUMP) show current_version)\n"

bump-version:
	@if [[ -z "$(VERSION)" ]]; then printf "Usage: make bump-version VERSION=0.1.2\n"; exit 2; fi
	@$(MAKE) bump-version-files VERSION="$(VERSION)"
	@$(MAKE) deploy

bump-minor-version:
	@$(MAKE) bump-version-files PART=minor
	@$(MAKE) deploy

bump-patch-version:
	@$(MAKE) bump-version-files PART=patch
	@$(MAKE) deploy
