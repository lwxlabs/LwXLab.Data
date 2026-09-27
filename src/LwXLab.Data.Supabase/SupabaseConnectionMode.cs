namespace LwXLab.Data.Supabase;

public enum SupabaseConnectionMode
{
    Unknown = 0,
    Direct = 1,
    SessionPooler = 2,
    TransactionPooler = 3
}
