namespace HookSentry.Api.DataTransfer.Tenants.Requests;

public record VerifySignatureRequest(string Payload, string Signature);
