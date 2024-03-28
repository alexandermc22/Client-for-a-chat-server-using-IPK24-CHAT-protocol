.PHONY: all clean

all: ipk24chat-client

ipk24chat-client: $(wildcard Client/**/*.cs)
    dotnet build -o ipk24chat-client

clean:
    rm -rf ipk24chat-client