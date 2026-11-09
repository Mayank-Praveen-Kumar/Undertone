using Undertone.Models;

namespace Undertone.Services;

/// <summary>
/// Lightweight audit trail recording for library and metadata changes.
/// </summary>
public interface IAuditTrailService
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default);
}

