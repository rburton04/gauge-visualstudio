﻿// Copyright [2014, 2015] [ThoughtWorks Inc.](www.thoughtworks.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Gauge.CSharp.Core;
using Gauge.Messages;
using Gauge.VisualStudio.Core;
using Gauge.VisualStudio.Core.Exceptions;

namespace Gauge.VisualStudio.Model
{
    public class GaugeServiceClient : IGaugeServiceClient
    {
        private readonly IGaugeService _gaugeService;

        public GaugeServiceClient(IGaugeService gaugeService)
        {
            _gaugeService = gaugeService;
        }

        public GaugeServiceClient() : this(GaugeService.Instance)
        {
        }

        public string GetParsedStepValueFromInput(EnvDTE.Project project, string input)
        {
            var stepValueFromInput = GetStepValueFromInput(project, input);
            return stepValueFromInput == null ? string.Empty : stepValueFromInput.StepValue;
        }

        public string GetFindRegex(EnvDTE.Project project, string input)
        {
            if (input.EndsWith(" <table>", StringComparison.Ordinal))
            {
                input = input.Remove(input.LastIndexOf(" <table>", StringComparison.Ordinal));
            }
            var parsedValue = GetParsedStepValueFromInput(project, input);
            parsedValue = parsedValue.Replace("* ", "");
            return string.Format(@"^(\*[ |\t]*|[ |\t]*\[Step\(""){0}\s*(((\r?\n\s*)+\|([\w ]+\|)+)|(<table>))?(""\)\])?\r?\n", parsedValue.Replace("{}", "((<|\")(?!<table>).+(>|\"))"), Parser.TableRegex);
        }

        public static long GenerateMessageId()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public ProtoStepValue GetStepValueFromInput(EnvDTE.Project project, string input)
        {
            try
            {
                var gaugeApiConnection = _gaugeService.GetApiConnectionFor(project);
                var stepsRequest = GetStepValueRequest.CreateBuilder().SetStepText(input).Build();
                var apiMessage = APIMessage.CreateBuilder()
                    .SetMessageId(GenerateMessageId())
                    .SetMessageType(APIMessage.Types.APIMessageType.GetStepValueRequest)
                    .SetStepValueRequest(stepsRequest)
                    .Build();

                var bytes = gaugeApiConnection.WriteAndReadApiMessage(apiMessage);
                return bytes.StepValueResponse.StepValue;
            }
            catch (GaugeApiInitializationException)
            {
                return default(ProtoStepValue);
            }
        }

        public IEnumerable<ProtoStepValue> GetAllStepsFromGauge(EnvDTE.Project project)
        {
            try
            {
                var gaugeApiConnection = _gaugeService.GetApiConnectionFor(project);
                var stepsRequest = GetAllStepsRequest.DefaultInstance;
                var apiMessage = APIMessage.CreateBuilder()
                    .SetMessageId(GenerateMessageId())
                    .SetMessageType(APIMessage.Types.APIMessageType.GetAllStepsRequest)
                    .SetAllStepsRequest(stepsRequest)
                    .Build();

                var bytes = gaugeApiConnection.WriteAndReadApiMessage(apiMessage);
                return bytes.AllStepsResponse.AllStepsList;

            }
            catch (GaugeApiInitializationException)
            {
                return Enumerable.Empty<ProtoStepValue>();
            }
        }

        public IEnumerable<ProtoSpec> GetSpecsFromGauge(IGaugeApiConnection apiConnection)
        {
            var specsRequest = SpecsRequest.DefaultInstance;
            var apiMessage = APIMessage.CreateBuilder()
                .SetMessageId(GenerateMessageId())
                .SetMessageType(APIMessage.Types.APIMessageType.SpecsRequest)
                .SetSpecsRequest(specsRequest)
                .Build();

            var bytes = apiConnection.WriteAndReadApiMessage(apiMessage);

            var specs = bytes.SpecsResponse.DetailsList.Where(detail => detail.HasSpec).Select(detail => detail.Spec);
            return specs;
        }

        public IEnumerable<ProtoSpec> GetSpecsFromGauge(int apiPort)
        {
            return GetSpecsFromGauge(new GaugeApiConnection(new TcpClientWrapper(apiPort)));
        }
    }
}