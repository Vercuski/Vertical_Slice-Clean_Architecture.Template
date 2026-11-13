// Application entry point (top-level statements): configures services and the HTTP pipeline, then runs the API.
// AddInfrastructureRegistration() also registers persistence (connection strings and the data-access
// services the slices use); AddSlices / MapSlices discover and map every feature slice.

using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Vertical_Slice_Clean_Architecture.Infrastructure;
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

var builder = WebApplication.CreateBuilder(args);

builder.AddInfrastructureRegistration();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Every handler, endpoint and validator in the assembly.
builder.Services.AddSlices(typeof(Program).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline.
// The OpenAPI document (/openapi/v1.json) and the Scalar API reference UI (/scalar) are only
// exposed in the Development environment.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("Vertical-Slice_Clean-Architecture API")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

// The correlation ID middleware runs first, so error responses and every log entry carry the ID.
app.UseCorrelationIdMiddleware();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.AddInfrastructureApplicationRegistration();
app.UseHttpsRedirection();

app.MapSlices();

app.Run();
