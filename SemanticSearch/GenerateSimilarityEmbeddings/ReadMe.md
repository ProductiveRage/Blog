# Semantic Textual Similarity Embedding Model (aka. Semantic Vector Search) Demo

The code in this project will build an in-memory semantic search index for the contents of the blog posts in this repo.

If you run the code, it will read the blog post markdown files, chunk up the content, vectorise it (using the [bge-micro-v2](https://huggingface.co/TaylorAI/bge-micro-v2) model, which it will download) and allow you to enter queries to execute against the content.

It will only download the model the first time that you run it (and will write the data to the project output folder, for use on subsequent runs) and only generate the embeddings the first time (again, persisting the embeddings data to disk in the project output folder).

It uses Microsoft's [Semantic Kernel](https://github.com/microsoft/semantic-kernel) library (much of which is currently in preview, as of March 2025) and downloads the embedding model from [Hugging Face](http://huggingface.co/). If you wanted to use this code to experiment with a larger data set, there are other persistence mechanisms available - such as connectors to Postgres, Redis, SQL Server and various others (see [Out-of-the-box Vector Store connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/) for more details).