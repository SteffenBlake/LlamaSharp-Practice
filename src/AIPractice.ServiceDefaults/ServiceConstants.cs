namespace AIPractice.ServiceDefaults;

public static class ServiceConstants 
{
    private const string PREF =         "AIPRACTICE-";
    public const string POSTGRES =      PREF+nameof(POSTGRES); 
    public const string POSTGRESDB =    PREF+nameof(POSTGRESDB); 
    public const string QDRANT = 		PREF+nameof(QDRANT); 
    public const string RABBITMQ = 		PREF+nameof(RABBITMQ); 
    public const string KAFKA = 		PREF+nameof(KAFKA); 
    public const string CHAT = 			PREF+nameof(CHAT); 
    public const string DATAWORKER = 	PREF+nameof(DATAWORKER); 
    public const string DOCINGESTER =	PREF+nameof(DOCINGESTER); 
    public const string MODELWORKER =	PREF+nameof(MODELWORKER); 
    public const string WEBAPI = 		PREF+nameof(WEBAPI); 
    public const string SVELTECHAT = 	PREF+nameof(SVELTECHAT); 
}
