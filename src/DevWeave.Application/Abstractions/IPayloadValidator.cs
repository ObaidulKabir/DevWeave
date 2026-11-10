using DevWeave.Domain.Schema;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Port for the JSON Schema (Draft 2020-12) validation engine (SRS SCH-002, KNW-004). The concrete
/// implementation lives in the Infrastructure layer so Domain and Application remain free of the
/// JSON-Schema library. An empty result means the payload is valid; otherwise the returned list
/// carries path-specific issues in a stable, ordered form.
/// </summary>
public interface IPayloadValidator
{
    IReadOnlyList<SchemaValidationIssue> Validate(string contentJson, string schemaJson);
}