﻿using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PortfolioFollow.Service.Commons;
using PortfolioFollow.Common.Interfaces;
using PortfolioFollow.Domain.Classes;
using PortfolioFollow.Api.Models;

namespace PortfolioFollow.Controllers
{
    [Route("api/preço")]
    [ApiController]
    public class AssetPriceController : ControllerBase
    {
        private readonly IAssetPriceBusiness _assetPriceBusiness;
        private readonly IOptions<Configurations> _config;

        public AssetPriceController(IAssetPriceBusiness assetPriceBusiness, IOptions<Configurations> config)
        {
            _assetPriceBusiness = assetPriceBusiness;
            _config = config;
        }

        [HttpGet("renda-variavel/ticker/{ticker}")]
        public ActionResult<string> Get(string ticker)
        {
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() },
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                    {
                        OverrideSpecifiedNames = false
                    }
                }
            };

            return JsonConvert.SerializeObject(_assetPriceBusiness.FindPrice(AssetType.RV, ticker), settings);
        }

        [HttpGet]
        [Route("tesouro-direto")]
        public async Task<string> GetFixedIncomeAsync()
        {
            var result = new List<Asset>();
            HttpClient client = new HttpClient();

            var requestReturn = await client.GetStringAsync("http://www.tesouro.gov.br/web/stn/tesouro-direto-precos-e-taxas-dos-titulos");

            var regexLine = new Regex(@"<tr[\s\S]*?<\/tr>");

            foreach (var line in regexLine.Matches(requestReturn))
            {
                var regexElement = new Regex(@"<td[^>](.+?)<\/td>");
                var regexClean = new Regex(@"[R$]|>|<");

                var elements = regexElement.Matches(line.ToString());

                if (elements.Count() == 4)
                {
                    var regexContent = new Regex(@">([^<]*)<");

                    var asset = new Asset
                    {
                        Type = AssetType.RF,
                        Symbol = regexClean.Replace(regexContent.Match(elements[0].Value).Value, ""),
                        SettlementDate = DateTime.Parse(regexClean.Replace(regexContent.Match(elements[1].Value).Value, "")),
                        Prices = new List<AssetPrice>
                        {
                            new AssetPrice
                            {
                                Date = DateTime.Now,
                                Price = decimal.Parse(regexClean.Replace(regexContent.Match(elements[3].Value).Value, ""))
                                
                            }
                        },
                        ProfitLoss = decimal.Parse(regexClean.Replace(regexContent.Match(elements[2].Value).Value, "")),
                    };

                    result.Add(asset);
                }
            }

            return JsonConvert.SerializeObject(result);
        }

        [HttpGet]
        [Route("renda-fixa")]
        public async Task<object> GetPrivateFixedIncomeAsync(decimal percentualCdi, decimal valorAplicado, DateTime dataInicio, DateTime? dataFim = null)
        {
            HttpClient client = new HttpClient();

            var requestReturn = await client.GetStringAsync(
                $"https://calculadorarendafixa.com.br/calculadora/di/calculo" +
                    $"?dataInicio={dataInicio.ToString("yyyy-MM-dd")}" +
                    $"&dataFim={(dataFim ?? DateTime.Now).ToString("yyyy-MM-dd")}" +
                    $"&percentual={percentualCdi.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}" +
                    $"&valor={valorAplicado.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");

            return JsonConvert.DeserializeObject<object>(requestReturn);
        }

        [HttpPost]
        public void Post([FromBody]Domain.AssetPrice assetPrice)
        {
            _assetPriceBusiness.Insert(assetPrice);
        }
    }
}
