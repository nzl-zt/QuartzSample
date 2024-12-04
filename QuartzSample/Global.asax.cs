using Quartz.Impl;
using Quartz;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;

namespace QuartzSample
{
    public class Global : System.Web.HttpApplication
    {

        private IScheduler _scheduler;

        protected void Application_Start(object sender, EventArgs e)
        {
            
            string connectionString = ConfigurationManager.AppSettings["DatabaseConnection"];

            // Start the Quartz Scheduler
            var schedulerFactory = new StdSchedulerFactory();
            _scheduler = schedulerFactory.GetScheduler().Result;
            _scheduler.Start();

            // Define the job details
            IJobDetail job = JobBuilder.Create<ExpiryNotificationJob>()
                .WithIdentity("DailyNotificationJob", "NotificationGroup")
                .UsingJobData("connectionString", connectionString) // Pass the connection string to the job
                .Build();

            // Define a trigger with a Cron expression
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("DailyNotificationTrigger", "NotificationGroup")
                .StartNow()
                .WithCronSchedule("0 0/1 * * * ?")
                .Build();

            // Schedule the job with the trigger
            _scheduler.ScheduleJob(job, trigger);
        }



        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {
            _scheduler?.Shutdown();
        }
    }
}
