using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace mini_projekt_stepan
{
    internal class CzechPricesLocalisator
    {
        //public async void Run()
        public void Run()
        {
            while (true) 
            {
                Console.WriteLine("Input date");
                var input = Console.ReadLine();

                if (TryParseUserDate(input, out DateTime inputDate))
                {
                    //var USDRate = GetCurrentUSDRate(inputDate);
                    var USDRate = GetCurrentUSDRate(inputDate);
                    if (float.TryParse(USDRate.ToString(), out float floatUSDRate))
                    {
                        EstablishConnectionToDB(floatUSDRate, inputDate);
                    }
                    Console.WriteLine("Would you like to know the USD rate for a different date? If yes type (y)");
                    var more = Console.ReadLine();
                    if (more != "y")
                    {
                        break;
                    }
                }
                else
                {
                    Console.WriteLine($"Would you like to try again? If yes type (y)");
                    var more = Console.ReadLine();
                    if (more != "y")
                    {
                        break;
                    }
                }

            }
        }
        static bool TryParseUserDate(string input, out DateTime inputDate)
        {
            if (DateTime.TryParse(input.Trim(), out DateTime userDate) && 0 >= userDate.CompareTo(DateTime.Today))
            {
                inputDate = userDate;
                return true;
            }
            Console.WriteLine("Date out of range. Can't find a rate for a date in the future");
            inputDate = DateTime.MaxValue;
            return false;
        }
        //static async Task<string> GetData(DateTime inputDate)
        static string GetData(DateTime inputDate)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var url = "https://www.cnb.cz/cs/financni-trhy/devizovy-trh/kurzy-devizoveho-trhu/kurzy-devizoveho-trhu/rok.txt?rok=" + inputDate.Year;
                    //HttpResponseMessage response = await client.GetAsync(url);
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        //var data = await response.Content.ReadAsStringAsync();
                        var data = response.Content.ReadAsStringAsync().Result;
                        return data;
                    }
                    else
                    {
                        throw new Exception("Failed to retrieve ČNB rate data: " + response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }
        //public async Task<string> GetCurrentUSDRate(DateTime inputDate)
        public string GetCurrentUSDRate(DateTime inputDate)
        {
            //var data = await GetData(inputDate);
            var data = GetData(inputDate);
            var refactoredData = data.ToString().Split('\n');
            var USDRate = "";
            var currencyExchangeDate = DateTime.MinValue;

            for (var i = 1; i < refactoredData.Length; i++)
            {
                try
                {
                    var ratesForOneDate = refactoredData[i].Split('|');
                    if (DateTime.TryParse(ratesForOneDate[0], new CultureInfo("cs-CZ"), DateTimeStyles.None, out DateTime singleExchangeDate) && inputDate > singleExchangeDate)
                    {
                        currencyExchangeDate = singleExchangeDate;
                        USDRate = ratesForOneDate[29];
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
            Console.WriteLine($"CZK to USD rate for date {inputDate} was found it is {USDRate}. Referenced currency exchange rate date is {currencyExchangeDate}");
            return USDRate;
        }
        static void EstablishConnectionToDB(float USDRate, DateTime date)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder();
                builder.DataSource = "stbechyn-sql.database.windows.net";
                builder.UserID = "prvniit";
                builder.Password = "P@ssW0rd!";
                builder.InitialCatalog = "AdventureWorksDW2020";

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    var sql = "select \"EnglishProductName\", round(\"StandardCost\", 0) as DealerPriceUSD, round(\"StandardCost\" * @USDRate, 0) as DealerPriceCZK from DimProduct order by \"EnglishProductName\"";
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@USDRate", USDRate);
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            SaveCZLocalisedData(reader, USDRate, date);
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void SaveCZLocalisedData(SqlDataReader reader, float USDRate, DateTime date)
        {
            using (StreamWriter writetext = new StreamWriter($"./yyyymmdd-adventureworks.csv"))
            {
                writetext.WriteLine("{0} {1} {2} {3} {4}", "EnglishProductName", "DealerPriceUSD", "DealerPriceCZK", "Date", "RateForThatDate");
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        writetext.WriteLine("{0} {1} {2} {3} {4}", reader.GetString(0), reader.GetDecimal(1), reader.GetDouble(2), date, USDRate.ToString("n", CultureInfo.GetCultureInfo("cz")));
                        //reader.GetDouble() is used instead of GetDecimal() because round() in the SQL query does not convert the type. So reader.GetDouble(2)  
                        //is still of type float so GetDecimal() can't be used because GetDecimal() does not allow for decimal numerals
                    }
                }
                Console.WriteLine("Query result successfully printed! Check your Debug folder.");
                Console.WriteLine("Rows with null values in StandardCost column were ignored.");
            }
        }
    }
}