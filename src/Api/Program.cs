using LeaveWorkforce.Audit.Contracts;
using LeaveWorkforce.Leave.Contracts;
using LeaveWorkforce.People.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAudit();
builder.Services.AddLeave();
builder.Services.AddPeople();

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "up" }));

app.MapAuditEndpoints();
app.MapLeaveEndpoints();
app.MapPeopleEndpoints();

app.Run();

public partial class Program;
