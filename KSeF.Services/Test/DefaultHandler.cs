using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KSeF.Client.Core.Interfaces.Rest;

namespace KSeF.Services.Test
{
	//Obsługuje żądania GET
	[HandlesRequest("*")]
	internal class DefaultHandler: IRequestHandler
	{
		protected IServiceProvider? _services; 
		protected IRestClient? _restClient; 
		protected string _request = String.Empty;
		protected string _response = String.Empty;


		public void Assign(string request, IServiceProvider services)
		{
			_request = request;
			_services = services;
			_restClient = services.GetRequiredService<IRestClient>();
		}

		public bool RequiresInput { get { return false; } }

		public bool HasResults { get { return true; } }

		public Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			throw new NotImplementedException();
		}

		public async Task ProcessAsync(CancellationToken stopToken)
		{
			if (_restClient == null) return; //dodane tylko po to, by linia poniżej nie generowało ostrzeżenia:

			_response = await _restClient.SendAsync<string, object>(HttpMethod.Get, 
																	_request, additionalHeaders: null,
																	cancellationToken:stopToken).ConfigureAwait(false);
			return;
		}

		public string SerializeResults()
		{
			return _response;
		}


	}
}
