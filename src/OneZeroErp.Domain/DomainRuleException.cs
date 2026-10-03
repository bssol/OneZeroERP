namespace OneZeroErp.Domain;

public sealed class DomainRuleException(string message) : InvalidOperationException(message);