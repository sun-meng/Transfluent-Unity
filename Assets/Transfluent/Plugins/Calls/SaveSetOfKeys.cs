﻿using Pathfinding.Serialization.JsonFx;
using System;
using System.Collections.Generic;
using transfluent;

namespace Assets.Transfluent.Plugins.Calls
{
	[Route("texts", RestRequestType.POST, "http://transfluent.com/backend-api/#Texts")] //expected return type?
	public class SaveSetOfKeys : WebServiceParameters
	{
		public SaveSetOfKeys(int language, Dictionary<string, string> dictionaryToSave, string group_id = null)
		{
			if(language <= 0) throw new Exception("INVALID Language in getAllExistingKeys");

			getParameters.Add("language", language.ToString());

			if(!string.IsNullOrEmpty(group_id))
			{
				getParameters.Add("groupid", group_id);
			}
			postParameters.Add("texts", JsonWriter.Serialize(dictionaryToSave));
		}

		[Inject(NamedInjections.API_TOKEN)]
		public string authToken { get; set; }

		public Dictionary<string, TransfluentTranslation> Parse(string text)
		{
			return GetResponse<Dictionary<string, TransfluentTranslation>>(text);
		}
	}
}