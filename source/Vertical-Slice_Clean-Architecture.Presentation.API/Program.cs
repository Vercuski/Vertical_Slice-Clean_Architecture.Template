// Application entry point (top-level statements): configures services and the HTTP pipeline, then runs the API.
// Note: the vertical-slice wiring in Common/SliceInfrastructure.cs (AddSlices / MapSlices) and the
// SampleDbContext registration are not called here yet, so the /Sample endpoints are not mapped.

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
// The OpenAPI document (/openapi/v1.json) is only exposed in the Development environment.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Sample data for the default ASP.NET Core weather forecast endpoint below.
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// GET /weatherforecast: returns five days of randomly generated forecasts, starting tomorrow.
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

/// <summary>
/// A single day's forecast returned by <c>GET /weatherforecast</c>.
/// </summary>
/// <param name="Date">The calendar date the forecast applies to.</param>
/// <param name="TemperatureC">The temperature in degrees Celsius.</param>
/// <param name="Summary">A one-word description of the weather, if any.</param>
internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    /// <summary>
    /// Gets the temperature in degrees Fahrenheit, converted from <see cref="TemperatureC"/> and truncated to an integer.
    /// </summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
