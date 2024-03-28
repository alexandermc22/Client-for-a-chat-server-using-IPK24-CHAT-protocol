BUILD_DIR := bin
OUTPUT_NAME := Client
CS_PROJ := Client/Client.csproj
LAUNCHER_NAME := ipk24chat-client

all: build launcher

build:
	dotnet build $(CS_PROJ) -o $(BUILD_DIR)

launcher:
	@echo '#!/bin/bash' > $(LAUNCHER_NAME)
	@echo 'dotnet $(BUILD_DIR)/$(OUTPUT_NAME).dll "$$@"' >> $(LAUNCHER_NAME)
	@chmod +x $(LAUNCHER_NAME)

clean:
	rm -rf $(BUILD_DIR)
	rm -f $(LAUNCHER_NAME)