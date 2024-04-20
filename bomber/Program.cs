﻿using System.Net.Http.Json;
using System.Text.Json;
using bomber;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using Simple;

const string grpcUrl = "http://localhost:50051";
const string httpUrl = "http://localhost:3000";

// Set minimum thread pool size
ThreadPool.SetMinThreads(300, 300);

var services = new ServiceCollection();

// Configure gRPC client using GrpcClientFactory
services.AddGrpcClient<BarService.BarServiceClient>("simple", options => { options.Address = new Uri(grpcUrl); });
services.AddHttpClient("simple_http", client => client.BaseAddress = new Uri(httpUrl));

var serviceProvider = services.BuildServiceProvider();
var grpcFactory = serviceProvider.GetRequiredService<GrpcClientFactory>();
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

// Define a scenario
var grpcScenario = Scenario.Create("scenario_grpc", async context =>
{
    try
    {
        var grpcBars = await grpcFactory.CreateClient<BarService.BarServiceClient>("simple").GetBarsAsync(new Empty());
        return Response.Ok<BarsResponse>(payload: grpcBars);
    }
    catch (Exception)
    {
        return Response.Fail();
    }
});
var httpScenario = Scenario.Create("scenario_http", async context =>
{
    HttpResponseMessage response;
    try
    {
        response = await httpClientFactory.CreateClient("simple_http").GetAsync("/bars");
    }
    catch (Exception )
    {
        return Response.Fail();
    }
    if (!response.IsSuccessStatusCode) return Response.Fail();
    var bars = await response.Content.ReadFromJsonAsync<JsonBarsResponse>();
    if (bars == null) return Response.Fail();
    return Response.Ok<JsonBarsResponse>(payload: bars);
});
var httpStaticScenario = Scenario.Create("scenario_http_static", async context =>
{
    HttpResponseMessage response;
    try
    {
        response = await httpClientFactory.CreateClient("simple_http").GetAsync("/bars-static");
    }
    catch (Exception )
    {
        return Response.Fail();
    }
    if (!response.IsSuccessStatusCode) return Response.Fail();
    var bars = await response.Content.ReadFromJsonAsync<JsonBarsResponse>();
    if (bars == null) return Response.Fail();
    return Response.Ok<JsonBarsResponse>(payload: bars);
});

// Define load simulation
var loadSimulation1 = Simulation.KeepConstant(1, TimeSpan.FromMinutes(2));
var loadSimulation50 = Simulation.KeepConstant(50, TimeSpan.FromMinutes(2));
var loadSimulation200 = Simulation.KeepConstant(200, TimeSpan.FromMinutes(2));

// Run scenarios
RunSimulation(httpScenario, loadSimulation1);
RunSimulation(httpStaticScenario, loadSimulation1);

RunSimulation(httpScenario, loadSimulation50);
RunSimulation(httpStaticScenario, loadSimulation50);

RunSimulation(httpScenario, loadSimulation200);
RunSimulation(httpStaticScenario, loadSimulation200);

static void RunSimulation(ScenarioProps scenarioProps, LoadSimulation loadSimulation)
{
    NBomberRunner.RegisterScenarios(scenarioProps.WithLoadSimulations(loadSimulation))
        .WithTargetScenarios(scenarioProps.ScenarioName)
        .Run();
    
}