An example of querying a feed using Redis. (Redis with Query == Reqis)

# Getting Started
1. Run a Redis server. I use Docker: https://github.com/dockerfile/redis
2. Build the solution.
3. Run the Indexer and the Enquirer.

# How It Works
## Indexer
The job of the indexer is to use an incoming feed and populate the sets that represent an object matching a query aspect.
## Enquirer
The job of the enquirer is to listen for updates and produce a feed of events describing with objects need to be added to the matching set and which objects need to be removed.
