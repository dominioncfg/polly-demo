using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddHttpClient<IUserResilientTestService, UserResilientTestService>(options =>
    {
        options.BaseAddress = new Uri("http://localhost:5115");
    })
    .AddStandardResilienceHandler();

//builder.Services.AddScoped<IUserResilientTestService, UserResilientTestService>();
builder.Services.AddResiliencePipeline<string, UserResponse>("ioc-registered-pipeline",
    pipelineBuilder =>
    {
        pipelineBuilder
        .AddFallback(new Polly.Fallback.FallbackStrategyOptions<UserResponse>()
        {
            FallbackAction = _ => Outcome.FromResultAsValueTask(new UserResponse(Guid.Empty, "FromIoCUser"))
        });
    });


builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
