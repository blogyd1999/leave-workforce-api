using LeaveWorkforce.Audit.Contracts;
using LeaveWorkforce.BuildingBlocks.Results;
using LeaveWorkforce.Leave.Contracts;
using LeaveWorkforce.People.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Detail = null;
            context.ProblemDetails.Extensions["code"] = ErrorCodes.InternalError;
        }
    });

builder.Services.AddAudit();
builder.Services.AddLeave();
builder.Services.AddPeople();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Json(new { status = "up" }));

app.MapAuditEndpoints();
app.MapLeaveEndpoints();
app.MapPeopleEndpoints();

app.Run();

public partial class Program;
