﻿using System;
using System.IO;
using System.Reflection;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.BotFramework;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Polly;
using Refit;
using TeamsTalentMgmtAppV4.Bot;
using TeamsTalentMgmtAppV4.Bot.Dialogs;
using TeamsTalentMgmtAppV4.Infrastructure;
using TeamsTalentMgmtAppV4.Models;
using TeamsTalentMgmtAppV4.Services;
using TeamsTalentMgmtAppV4.Services.Interfaces;
using TeamsTalentMgmtAppV4.Services.Templates;
using TeamTalentMgmtApp.Shared.AutoMapper;
using TeamTalentMgmtApp.Shared.Services.Data;
using TeamTalentMgmtApp.Shared.Services.Interfaces;

namespace TeamsTalentMgmtAppV4
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);
            var appSettings = Configuration.Get<AppSettings>();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                {
                    builder.AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowAnyOrigin();
                });
            });

            services.AddRefitClient<IConnectorService>()
                .ConfigureHttpClient(x => x.BaseAddress = new Uri("https://outlook.office.com/webhook"))
                .AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.RetryAsync(3));

            services.AddSingleton(bc => NullBotTelemetryClient.Instance);

            // Configure credentials
            services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();
            services.AddSingleton(new MicrosoftAppCredentials(appSettings.MicrosoftAppId, appSettings.MicrosoftAppPassword));

            // Configure adapters
            services.AddSingleton<IBotFrameworkHttpAdapter, TeamsBotHttpAdapter>();

            // Configure storage
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<UserState>();
            services.AddSingleton<ConversationState>();

            services.AddDbContext<DatabaseContext>();

            // Dialogs.
            services.AddTransient<CandidateDetailsDialog>();
            services.AddTransient<CandidateSummaryDialog>();
            services.AddTransient<HelpDialog>();
            services.AddTransient<MainDialog>();
            services.AddTransient<NewJobPostingDialog>();
            services.AddTransient<OpenPositionsDialog>();
            services.AddTransient<PositionsDetailsDialog>();
            services.AddTransient<SignOutDialog>();
            services.AddTransient<TopCandidatesDialog>();
            services.AddTransient<NewTeamDialog>();
            services.AddTransient<InstallBotDialog>();

            // Templates
            services.AddTransient<CandidatesTemplate>();
            services.AddTransient<PositionsTemplate>();
            services.AddTransient<NewJobPostingToAdaptiveCardTemplate>();

            // Services
            services.AddTransient<IRecruiterService, RecruiterService>();
            services.AddTransient<ICandidateService, CandidateService>();
            services.AddTransient<IPositionService, PositionService>();
            services.AddTransient<ILocationService, LocationService>();
            services.AddTransient<IInterviewService, InterviewService>();
            services.AddTransient<INotificationService, NotificationService>();
            services.AddTransient<IBotService, BotService>();
            services.AddTransient<IGraphApiService, GraphApiService>();
            services.AddTransient<IInvokeActivityHandler, InvokeActivityHandler>();

            services.AddHttpClient();

            services.AddTransient<IBot, TeamsTalentMgmtBot>();
            services
                .AddMvc()
                .AddJsonOptions(options => { options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore; })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddAutoMapper(typeof(TeamsTalentAppBaseProfile), typeof(TeamsTalentMgmtProfile));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "StaticViews")),
                RequestPath = "/StaticViews"
            });

            app.UseCors("CorsPolicy");
            app.UseStatusCodePages();
            app.UseMvc();

            using (var serviceScope = app.ApplicationServices.CreateScope())
            {
                var scopeServiceProvider = serviceScope.ServiceProvider;
                var db = scopeServiceProvider.GetService<DatabaseContext>();
                var currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var dataSeedPath = Path.Combine(currentDirectory, "SampleData");
                SampleData.InitializeDatabase(dataSeedPath, db);
            }
        }
    }
}
