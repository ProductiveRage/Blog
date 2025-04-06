using MessagePack;
using Microsoft.Extensions.VectorData;

namespace GenerateSimilarityEmbeddings;

[MessagePackObject]
public sealed record IndexablePostChunk(
    [property: VectorStoreRecordKey, Key(0)] int Id,
    [property: VectorStoreRecordData, Key(1)] int PostId,
    [property: VectorStoreRecordData, Key(2)] string Text,
    [property: VectorStoreRecordVector(768), Key(3)] ReadOnlyMemory<float> Embedding); // e5-base-v2 generates vectors with 768 dimensions