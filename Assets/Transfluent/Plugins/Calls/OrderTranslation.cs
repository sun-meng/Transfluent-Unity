﻿using System;
using System.Collections.Generic;
using Pathfinding.Serialization.JsonFx;

namespace transfluent
{
	[Route("texts/translate", RestRequestType.GET, "http://transfluent.com/backend-api/#TextsTranslate")]
	public class OrderTranslation : WebServiceParameters
	{
		public enum TranslationQuality
		{
			PAIR_OF_TRANSLATORS = 3,
			PROFESSIONAL_TRANSLATOR = 2,
			NATIVE_SPEAKER = 1,
		}

		//group_id, source_language, target_languages, texts, comment, callback_url, max_words [=1000], level [=2], token
		[Inject(NamedInjections.API_TOKEN)]
		public string authToken { get; set; }


		public OrderTranslation(int source_language, int[] target_languages, string[] texts, string comment=null, int max_words = 1000, TranslationQuality level = TranslationQuality.PROFESSIONAL_TRANSLATOR,string group_id=null)
		{
			var containerOfTextIDsToUse = new List<TextIDToTranslateContainer>();
			foreach(string toTranslate in texts)
			{
				containerOfTextIDsToUse.Add(new TextIDToTranslateContainer
				{
					id = toTranslate
				});
			}

			getParameters.Add("source_language", source_language.ToString());
			getParameters.Add("target_languages", JsonWriter.Serialize(target_languages));
			getParameters.Add("texts", JsonWriter.Serialize(containerOfTextIDsToUse));

			if(level != 0)
			{
				getParameters.Add("level", ((int)level).ToString());
			}
			if(group_id != null)
			{
				getParameters.Add("group_id", group_id);
			}
			if(!string.IsNullOrEmpty(comment))
			{
				getParameters.Add("comment", comment);
			}
			if(max_words > 0)
			{
				getParameters.Add("max_words", max_words.ToString());
			}
		}
		public TextsTranslateResult Parse(string text)
		{
			return GetResponse<TextsTranslateResult>(text);
		}

		[Serializable]
		public class TextIDToTranslateContainer
		{
			public string id;
		}

		[Serializable]
		public class TextsTranslateResult
		{
			public int ordered_word_count;
			public int word_count;
		}

	}
}