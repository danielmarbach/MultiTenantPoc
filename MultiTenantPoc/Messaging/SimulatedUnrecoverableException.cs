namespace MultiTenantPoc;

public sealed class SimulatedUnrecoverableException(string message) : Exception(message);