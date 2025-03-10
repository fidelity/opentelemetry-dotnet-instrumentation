// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Net.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning);
        });
var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Logged before host is built.");
var builder = WebApplication.CreateBuilder(args);

using var app = builder.Build();
app.MapGet("/test", (ILogger<Program> logger) =>
{
    logger.LogInformation("Request received.");
    return "Hello World!";
});

app.Start();

var server = (IServer?)app.Services.GetService(typeof(IServer));
var addressFeature = server?.Features.Get<IServerAddressesFeature>();
var address = addressFeature?.Addresses.First();

using var httpClient = new HttpClient();
httpClient.GetAsync($"{address}/test").Wait();
