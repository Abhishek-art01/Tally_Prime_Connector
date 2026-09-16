namespace TallyPrimeConnector.Contracts;

public sealed class TallyConnectionException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class TallyProtocolException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class TallyCompanyException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ExtractionException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ProcessingException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ExportException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ConfigurationException(string message, Exception? inner = null) : Exception(message, inner);
