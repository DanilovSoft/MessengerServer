using Microsoft.EntityFrameworkCore.Migrations;

namespace DbMigrator.Extensions
{
    public static class CreateUserExpansion
    {
        internal static void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.gen_password(
)
    RETURNS trigger AS
$body$
BEGIN
    NEW.""Password"" = crypt(NEW.""Password"", gen_salt('bf'));
    RETURN NEW;
END;
$body$
    LANGUAGE 'plpgsql'
    VOLATILE
    CALLED ON NULL INPUT
    SECURITY INVOKER
    PARALLEL UNSAFE
    COST 100;
");
            migrationBuilder.Sql(@"ALTER FUNCTION public.gen_password () OWNER TO postgres;");

            migrationBuilder.Sql(@"
CREATE TRIGGER ""Users_tr""
    BEFORE INSERT OR UPDATE OF ""Password""
    ON public.""Users""
    FOR EACH ROW
EXECUTE PROCEDURE public.gen_password();
");
        }

        internal static void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop trigger IF EXISTS ""Users_tr"" on public.""Users"";");
            migrationBuilder.Sql(@"drop function IF EXISTS public.gen_password();");
        }
    }
}