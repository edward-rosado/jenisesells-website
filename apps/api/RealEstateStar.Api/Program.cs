var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();

// Make Program accessible for integration tests
public partial class Program;
