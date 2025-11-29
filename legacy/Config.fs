module Config

open Microsoft.Extensions.Configuration;

[<CLIMutable>]
type Comdirect_Api =
  {
    Client_Id : string
    Client_Secret : string
  }

[<CLIMutable>]
type YNAB_Api =
  {
    Secret : string
  }


[<CLIMutable>]
type Transfer =
  {
    YNAB_Budget : string
    YNAB_Account : string
    Comdirect_Account : string
    Days : int
  }


[<CLIMutable>]
type Config = {
  Comdirect_Api: Comdirect_Api 
  YNAB_Api : YNAB_Api
  Transfer : Transfer
}

let fetch () =
  let builder = (new ConfigurationBuilder())
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, true)
                    .AddEnvironmentVariables(); 
  let configurationRoot = builder.Build();
  configurationRoot.Get<Config>()  