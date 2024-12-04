using Quartz;
using System;
using System.Data.SqlClient;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon;
using System.Threading.Tasks;
using NLog;
using Quartz.Logging;

namespace QuartzSample
{
    public class ExpiryNotificationJob : IJob
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                // Retrieve the connection string from the job data map
                var connectionString = context.JobDetail.JobDataMap.GetString("connectionString");

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Query to get records where ExpiryDate matches 3 months, 2 months, 1 month, 1 week, or 3 days before today
                    string query = @"
                    SELECT [ActivationCodes].[ID], 
                           [SetupInfo].[ContactEmail],
                           [SetupInfo].[ContactPerson],
                           [ActivationCodes].[ExpiryDate]
                    FROM [ActivationCodes] 
                    INNER JOIN [SetupInfo] 
                        ON [ActivationCodes].[ID] = [PPDSCastingSetupInfo].[ID]
                    WHERE CAST([ActivationCodes].[ExpiryDate] AS DATE) IN 
                    (@TargetDate3Months, @TargetDate2Months, @TargetDate1Month, @TargetDate1Week, @TargetDate3Days, @targetDate, @targetDate7DaysAfter)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Calculate the target dates (3 months, 2 months, 1 month, 1 week, and 3 days before expiry)
                        DateTime targetDate3MonthsBefore = DateTime.Today.AddMonths(3).Date;
                        DateTime targetDate2MonthsBefore = DateTime.Today.AddMonths(2).Date;
                        DateTime targetDate1MonthBefore = DateTime.Today.AddMonths(1).Date;
                        DateTime targetDate1WeekBefore = DateTime.Today.AddDays(7).Date;
                        DateTime targetDate3DaysBefore = DateTime.Today.AddDays(3).Date;
                        DateTime targetDate = DateTime.Today.Date;
                        DateTime targetDate7DaysAfter = DateTime.Today.AddDays(-7).Date;

                        // Add the calculated dates as parameters to the SQL query
                        command.Parameters.AddWithValue("@TargetDate3Months", targetDate3MonthsBefore);
                        command.Parameters.AddWithValue("@TargetDate2Months", targetDate2MonthsBefore);
                        command.Parameters.AddWithValue("@TargetDate1Month", targetDate1MonthBefore);
                        command.Parameters.AddWithValue("@TargetDate1Week", targetDate1WeekBefore);
                        command.Parameters.AddWithValue("@TargetDate3Days", targetDate3DaysBefore);
                        command.Parameters.AddWithValue("@targetDate", targetDate);
                        command.Parameters.AddWithValue("@targetDate7DaysAfter", targetDate7DaysAfter);

                        // Execute the query and read the results
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string unitId = reader.GetString(0);
                                string email = reader.GetString(1);
                                string contactPerson = reader.GetString(2);
                                DateTime ExpiryDate = reader.GetDateTime(3);

                                // Determine the remaining time for the expiry date
                                string timeLeft = GetTimeLeftMessage(ExpiryDate);

                                // Send email notification with the remaining time message
                                await SendEmailAsync(email, contactPerson, timeLeft, ExpiryDate);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any error that occurs during execution
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Method to send email asynchronously
        private async Task SendEmailAsync(string email, string contactPerson, string timeLeft,DateTime ExpiryDate)
        {
            using (var client = new AmazonSimpleEmailServiceClient(
                "",
                "",
                RegionEndpoint.EUCentral1))
            {
                var emailRequest = new SendEmailRequest
                {
                    Source = "test@test.test",
                    Destination = new Destination { ToAddresses = { email } },
                    Message = new Message
                    {
                        Subject = new Amazon.SimpleEmail.Model.Content("Confirm your license activation"),
                        Body = new Body
                        {
                            Text = new Amazon.SimpleEmail.Model.Content(
                                $"Dear {contactPerson},\n\n" +
                                 $"This is a reminder that your license {timeLeft}. Please take action before the expiration date to continue using the service without interruption.\n\n" +
                                $"License Expiration Date: {ExpiryDate.ToString("yyyy-MM-dd")}\n\n" +
                                "To complete the registration process and ensure uninterrupted service, please confirm your email address by clicking the link below:\n\n" +
                                "Action Required: Confirm your registration\n\n" +
                                "Best regards,\n" )
                        }
                    }
                };

                try
                {
                    // Send the email asynchronously
                    var response = await client.SendEmailAsync(emailRequest);

                    // Log the response
                    Logger.Trace($"Email successfully sent. HTTP Status Code: {response.HttpStatusCode}, Message ID: {response.MessageId}");
                }
                catch (Exception ex)
                {
                    // Log any errors during email sending
                    Logger.Error($"Email sending failed. Error: {ex.Message}");
                }
            }
        }

        // Method to determine the time left for the license expiration
        private string GetTimeLeftMessage(DateTime expiryDate)
        {
            TimeSpan timeRemaining = expiryDate.Date - DateTime.Today.Date;

            
            switch (timeRemaining.Days)
            {
                case 90:
                    return "will expire in 3 months";
                case 60:
                    return "will expire in 2 months";
                case 30:
                    return "will expire in 1 month";
                case 7:
                    return "will expire in 1 week";
                case 3:
                    return "will expire in 3 days";
                case 1:
                    return "will expire tomorrow";
                case 0:
                    return "expires today";
                case -1:
                    return "expired yesterday";
                case -7:
                    return "expired a week ago";
                default:
                    return null;
            }
        }
    }
}
