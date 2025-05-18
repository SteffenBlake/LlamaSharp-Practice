# Llamasharp Practice

## About this project

This is a .NET Aspire practice stack, meant to simulate several core principles:

1. .NET Aspire first class Azure supported "one click and deploy" style microservice architecture, leverage Aspire's ability to make it substantially easier for a developer to just stand up the application(s) and start debugging/testing

2. Leveraging RabbitMQ for message bus pattern between multiple "worker" services, some one-shot, some continuous.

3. Leveraging Apache Kafka for continuous data streams (in this case, Tokens) between 2 services

4. QDrant Vector Database for Semantic Memory Recall and fuzzy searching

5. LLamasharp + Microsoft.SemanticKernel to load up a provided LLM, and integrate with the Vector DB in #4

6. SignalR for Server pushed events to a front end client (continous token data and live chat with the LLM, UI updates, etc)

7. Svelte+Vite front end SPWA

8. Postgres + EF Core database with a backing singleton one-shot automatic migration (plus potential seed data if needed) service

9. Azure Blob Storage integration for cloud hosted LLM storage

10. Every individual piece designed to be individually scaleable on its own, if needed. Multiple instances of the LLM can be deployed and parallelized, to enable supporting multiple client connections, or parallelizing work processing documents for RAG'ing

11. Automatic service ordering, each individual service has its order of operation defined such that if service A depends on service B, it wont start up until A is healthy (this is a built in feature of Aspire)

12. **True** integration testing, Aspire lets us stand up the *entire* stack for integration testing against, top to bottom!

13. Reverse proxy support for custom hostname in 1 click, for when you are running your box on a host other than localhost and need to connect to any of the services from a separate machine (classic example being testing a web app or mobile app out on mobile phones, or if you are SSHing into your dev machine)

## Requirements to run
1. Dotnet SDK 8 or later installed
2. `docker` installed
3. A LLamasharp compatible LLM .gguf file downloaded (that your GPU can run!), with sufficient read/write permissions for your user.
4. Created directory for Azure Storage Emulator to store cached copies of models in with sufficient read/write permissions (`/var/opt/ModelWork` by default)
4. Configure `ModelPath` value in the `AIPractice.AppHost/appsettings.Development.json` to point to the file from step 3 
5. Configure `Model:CacheDir` in `AIPractice.ModelWorker/appsettings.Development.json` to point to the directory you made in step 4
6. Ensure the directory from step 5 exists and has sufficient Read/Write permissions for your user to access

## Running the application

1. `cd` into the AIPractice.AppHost project
2. `dotnet run`
3. console will give you the port the dashboard is running on, open this up to get info on every running microservice
4. You likely will want to open up the Svelte chat client's port, in order to start interacting with the frontend

Note: If you are connecting from a different machine and thus using a host other than localhost, you'll need to follow the steps below. Aspire doesn't support this behavior out of the box, and thus 1 extra step has to be done to enable the YARP dev proxy service ("DevProxy")

## Enabling proxying to machine's host (IE for connecting to Aspire apps from a secondary machine/mobile phone/etc)
1. `cd` into the Apphost project
2. Execute `dotnet user-secrets set "YARP:Host" "$(hostname)"` (works in bash and powershell!)
3. Instead of using the various URLs you see on the microservices on the dashboard, scroll down to the now added "DEVPROXY" service, and expand it's URLs section. Use these URLs instead to connect to the various services, as they have been proxied via a minimalist YARP project.

Note you can also substitute `$(hostname)` with some other hostname as you like, IE if you have a custom DNS or whatever setup in your network.

## Architecture

### AIPractice.AppHost

"Core" Aspire AppHost project, this orchestrates the entire stack automatically

### AIPractice.Chat (WIP)

"Frontend" Svelte SPWA, chat client

### AIPractice.DevProxy

Special "Dev Only" service that spins up if you have the `YARP:Host` config set on the AppHost. This project utilizes [YARP](https://github.com/dotnet/yarp) to stand up a minimalist reverse proxy to support connecting to Aspire using a host other than localhost. By default, to discourage trying to deploy Aspire itself, using a host other than localhost doesn't work for Aspire, at least, not easily. This YARP project however demo's a very easy way to get this behavior working in a controlled way, for example if you need to connect to an Aspire service from a secondary machine, like perhaps a mobile phone for testing.

### AIPractice.DataWorker

"One shot" startup service worker that will ensure the Postgres database exists, is migrated, and potentially has data seeded if needed

### AIPractice.DocumentIngester

"One shot" startup service worker that you configure with documents to download, as well as tag specific page ranges with metadata. You can configure it's `appsettings.Development.json` file if you like, though its default configured to download the entire Alberta's K-9 curriculum for math/science/ELA and tag numerous page ranges with the course subject and grade(s), as a handy default for searching on. Sends ingestion requests to the ModelWorker service for parsing into the Vector DB via RabbitMQ

### AIPractice.Domain

Core "domain" class library, 99% of the logic that matters resides in here.

### AIPractice.IntegrationTests (WIP)

Integration test project, lets you run true "top to bottom" tests against the entire stack

### AIPractice.ModelWorker

Continuous Microservice that hosts the LLM on local GPU VRAM resources. Uses RabbitMQ as interop for receiving all LLM related processing requests, already brokered across Aspire's built in scaleability. For outgoing token streams, utilizes Apache Kafka to send data to the WebAPI

### AIPractice.ServiceDefaults

Roslyn Source Gen project, used by Aspire for automatic service discovery

### AIPractice.UnitTests (WIP)

This is where I would keep my Atomic Unit Tests, if I had any (none made yet, WIP)

### AIPractice.WebApi

The RESTful WebAPI, made with ASP.NET. Hosts both normal API endpoints + a SignalR hub for piping data streams to the frontend chat client.
