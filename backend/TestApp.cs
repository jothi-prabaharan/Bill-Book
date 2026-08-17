using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main(string[] args)
    {
        string connStr = "Host=localhost;Port=5432;Database=EP_Tenant_Template;Username=postgres;Password=123";
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_org_id', $1, false)";
        var p = cmd.CreateParameter();
        p.Value = "";
        cmd.Parameters.Add(p);

        try 
        {
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("Success!");
        }
        catch (Exception ex) 
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
