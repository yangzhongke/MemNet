using BERTTokenizers.Base;
using MemNet.Abstractions;
using MemNet.Config;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MemNet.Embedders
{
    public class JinaEmbederV2ZH : IEmbedder
    {
        private InferenceSession? _inferenceSession;
        private MyTokenizer? tokenizer = null;
        private JinaEmbederConfig _config;

        public JinaEmbederV2ZH(IOptions<MemoryConfig> config)
        {
            _config = config.Value.JinaEmbedder;
        }

        public bool IsModelReady => _inferenceSession != null;

        public bool IsReady => IsModelReady && tokenizer != null;

        public void InitModel()
        {
            if (_inferenceSession != null)
            {
                return;
            }

            var sessionOptions = new SessionOptions
            {
                LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO
            };
            string model = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.Model);
            string vcab = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.Vocab);
            _inferenceSession = new InferenceSession(model, sessionOptions);
            tokenizer ??= new MyTokenizer(vcab);
        }
         private async Task<float[][]> GetEmbeddingsAsync(params string[] sentences)
        {
            if (!IsModelReady || !IsReady)
            {
                return Array.Empty<float[]>();
            }

             // 3.1) Tokenize the input text
             var encoded = tokenizer.CustomEncode(sentences);

            var input = new ModelInput
            {
                InputIds = encoded.Select(t => t.InputIds).ToArray(),
                AttentionMask = encoded.Select(t => t.AttentionMask).ToArray(),
            };

            var runOptions = new RunOptions();

            int sequenceLength = input.InputIds.Length / sentences.Length;

            // 3.2) Create input tensors over the input data.
            using var inputIdsOrtValue = OrtValue.CreateTensorValueFromMemory(input.InputIds,
                  [sentences.Length, sequenceLength]);

            using var attMaskOrtValue = OrtValue.CreateTensorValueFromMemory(input.AttentionMask,
                  [sentences.Length, sequenceLength]);


            var inputNames = new List<string>
            {
                "input_ids",
                "attention_mask",
            };

            var inputs = new List<OrtValue>
            {
                { inputIdsOrtValue },
                { attMaskOrtValue },
            };

            // 3.3) Create output tensors
            List<OrtValue> outputValues = [
                OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance,
                    TensorElementType.Float, [sentences.Length, sequenceLength, 768]),];

            try
            {
                // 3.4) Run the model
                var output = await _inferenceSession.RunAsync(runOptions, inputNames, inputs, _inferenceSession.OutputNames, outputValues);

                var firstElement = output.ToList()[0];
                var data = firstElement.GetTensorDataAsSpan<float>().ToArray();
                var typeAndShape = firstElement.GetTensorTypeAndShape();

                var sentence_embeddings = MeanPooling(data, input.AttentionMask, typeAndShape.Shape);

                var resultArray = NormalizeAndDivide(sentence_embeddings, typeAndShape.Shape);

                // 3.5) Split the result array into individual sentence embeddings
                return Enumerable.Chunk(resultArray, resultArray.Length / sentences.Length).ToArray();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return Array.Empty<float[]>();
            }
        }

        private static float[] MeanPooling(float[] embeddings, long[] attentionMask, long[] shape)
        {
            long batchSize = shape[0];
            int sequenceLength = (int)shape[1];
            int embeddingSize = (int)shape[2];
            float[] result = new float[batchSize * embeddingSize];

            for (int batch = 0; batch < batchSize; batch++)
            {
                Vector<float> sumMask = Vector<float>.Zero;
                Vector<float>[] sumEmbeddings = new Vector<float>[embeddingSize];

                for (int i = 0; i < embeddingSize; i++)
                    sumEmbeddings[i] = Vector<float>.Zero;

                for (int seq = 0; seq < sequenceLength; seq++)
                {
                    long mask = attentionMask[batch * sequenceLength + seq];
                    if (mask == 0)
                        continue;

                    for (int emb = 0; emb < embeddingSize; emb++)
                    {
                        int index = batch * sequenceLength * embeddingSize + seq * embeddingSize + emb;
                        float value = embeddings[index];
                        sumEmbeddings[emb] += new Vector<float>(value);
                    }
                    sumMask += new Vector<float>(1);
                }

                for (int emb = 0; emb < embeddingSize; emb++)
                {
                    float sum = Vector.Dot(sumEmbeddings[emb], Vector<float>.One);
                    float maskSum = Vector.Dot(sumMask, Vector<float>.One);
                    result[batch * embeddingSize + emb] = sum / maskSum;
                }
            }

            return result;
        }

        private static float[] NormalizeAndDivide(float[] sentenceEmbeddings, long[] shape)
        {
            long numSentences = shape[0];
            int embeddingSize = (int)shape[2];

            float[] result = new float[sentenceEmbeddings.Length];
            int vectorSize = Vector<float>.Count;

            // Compute Frobenius (L2) norms
            float[] norms = new float[numSentences];

            for (int i = 0; i < numSentences; i++)
            {
                Vector<float> sumSquares = Vector<float>.Zero;
                for (int j = 0; j < embeddingSize; j += vectorSize)
                {
                    int index = i * embeddingSize + j;
                    Vector<float> vec = new Vector<float>(sentenceEmbeddings, index);
                    sumSquares += vec * vec; // Element-wise squaring and summing
                }
                norms[i] = (float)Math.Sqrt(Vector.Dot(sumSquares, Vector<float>.One)); // Take square root of sum of squares
                norms[i] = Math.Max(norms[i], 1e-12f); // Clamping to avoid division by zero
            }

            // Normalize and divide
            for (int i = 0; i < numSentences; i++)
            {
                float norm = norms[i];
                for (int j = 0; j < embeddingSize; j += vectorSize)
                {
                    int index = i * embeddingSize + j;
                    Vector<float> vec = new Vector<float>(sentenceEmbeddings, index);
                    Vector<float> normalizedVec = vec / new Vector<float>(norm);
                    normalizedVec.CopyTo(result, index);
                }
            }

            return result;
        }
        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            return (await this.GetEmbeddingsAsync(text))[0];
        }

        public async Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default)
        {
            return (await this.GetEmbeddingsAsync(texts.ToArray())).ToList();
        }

        public async Task<int> GetVectorSizeAsync(CancellationToken ct = default)
        {
            if (_inferenceSession == null)
            {
                this.InitModel();
            }
            var testEmbedding = await EmbedAsync("test", ct);
            return testEmbedding.Length;
        }
    }
    public class ModelInput
    {
        public  long[] InputIds { get; init; }

        public  long[] AttentionMask { get; init; }
    }
    public class MyTokenizer(string vocabPath) : UncasedTokenizer(vocabPath)
    {
        public List<(long InputIds, long TokenTypeIds, long AttentionMask)> CustomEncode(params string[] texts)
        {
            List<List<int>> list = [];
            foreach (string text in texts)
            {
                List<string> innerList = ["<s>"];
                innerList.AddRange(TokenizeSentence(text));
                innerList.Add("</s>");
                list.Add(innerList.SelectMany(TokenizeSubwords).Select(s => s.VocabularyIndex).ToList());
            }

            int maxLength = list.Max(s => s.Count());

            for (int i = 0; i < list.Count; i++)
            {
                var innerList = list[i];
                if (maxLength - innerList.Count() > 0)
                {
                    list[i] = innerList.Concat(Enumerable.Repeat(0, maxLength - innerList.Count())).ToList();
                }
            }
            List<int> flatList = list.SelectMany(s => s).ToList();

            List<long> second = Enumerable.Repeat(0L, flatList.Count).ToList();
            List<long> third = AttentionMask(flatList);

            return flatList.Select((t, i) => ((long)t, second[i], third[i])).ToList();
        }

        private IEnumerable<(string Token, int VocabularyIndex)> TokenizeSubwords(string word)
        {
            if (_vocabularyDict.ContainsKey(word))
            {
                return new (string, int)[1] { (word, _vocabularyDict[word]) };
            }

            List<(string, int)> list = new List<(string, int)>();
            string text = word;
            while (!string.IsNullOrEmpty(text) && text.Length > 2)
            {
                string? text2 = null;
                int num = text.Length;
                while (num >= 1)
                {
                    string text3 = text.Substring(0, num);
                    if (!_vocabularyDict.ContainsKey(text3))
                    {
                        num--;
                        continue;
                    }

                    text2 = text3;
                    break;
                }

                if (text2 == null)
                {
                    list.Add(("<unk>", _vocabularyDict["<unk>"]));
                    return list;
                }

                text = new Regex(text2).Replace(text, "", 1);
                list.Add((text2, _vocabularyDict[text2]));
            }

            if (!string.IsNullOrWhiteSpace(word) && !list.Any())
            {
                list.Add(("<unk>", _vocabularyDict["<unk>"]));
            }

            return list;
        }

        private List<long> AttentionMask(List<int> tokens)
        {
            return tokens.Select(index => index == 0 ? (long)0 : 1).ToList();
        }
    }
}
