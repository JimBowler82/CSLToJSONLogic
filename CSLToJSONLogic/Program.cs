
using CSLToJSONLogic;

Console.WriteLine("Criteria Syntax Language To JSON Logic");
Console.Write("Enter a criteria string:");

try
{
    var result = "Result: ";
    var str = Console.ReadLine();

    Console.WriteLine(str);

    if (!string.IsNullOrEmpty(str))
    {
        var s = CriteriaToJSONLogic.ConvertToJSONLogic(str);

        result += s;
    }

    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.WriteLine("Error converting criteria string.");
    Console.Error.WriteLine(ex.Message);
    throw;
}






