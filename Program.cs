using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mini_projekt_stepan
{
    internal class Program
    {   
        static void Main(string[] args)
        {
            //DateTime userDate = DateTime.Today;//Simulating user input
            var app = new CzechPricesLocalisator();
            app.Run();
        }
        
    }
}
