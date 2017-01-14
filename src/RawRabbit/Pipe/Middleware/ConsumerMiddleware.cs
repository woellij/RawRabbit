﻿using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RawRabbit.Configuration.Consume;
using RawRabbit.Consumer;
using RawRabbit.Logging;

namespace RawRabbit.Pipe.Middleware
{
	public class ConsumerOptions
	{
		public Func<IPipeContext, ConsumeConfiguration> ConfigurationFunc { get; set; }
		public Func<IConsumerFactory, CancellationToken, IPipeContext, Task<IBasicConsumer>> ConsumerFunc { get; set; }
	}

	public class ConsumerMiddleware : Middleware
	{
		protected IConsumerFactory ConsumerFactory;
		protected Func<IPipeContext, ConsumeConfiguration> ConfigFunc;
		protected Func<IConsumerFactory, CancellationToken, IPipeContext, Task<IBasicConsumer>> ConsumerFunc;
		private readonly ILogger _logger = LogManager.GetLogger<ConsumerMiddleware>();

		public ConsumerMiddleware(IConsumerFactory consumerFactory, ConsumerOptions options = null)
		{
			ConsumerFactory = consumerFactory;
			ConfigFunc = options?.ConfigurationFunc ?? (context => context.GetConsumeConfiguration());
			ConsumerFunc = options?.ConsumerFunc ?? ((factory, token, context) => factory.CreateConsumerAsync(token: token));
		}

		public override Task InvokeAsync(IPipeContext context, CancellationToken token = default(CancellationToken))
		{
			return GetOrCreateConsumerAsync(context, token)
				.ContinueWith(tConsumer =>
					{
						var config = GetConfiguration(context);
						if (config != null)
						{
							ConsumerFactory.ConfigureConsume(tConsumer.Result, config);
						}
						context.Properties.TryAdd(PipeKey.Consumer, tConsumer.Result);
						return Next.InvokeAsync(context, token);
					}, token)
				.Unwrap();
		}

		protected virtual ConsumeConfiguration GetConfiguration(IPipeContext context)
		{
			var config = ConfigFunc(context);
			if (config == null)
			{
				_logger.LogInformation("Consumer configuration not found in Pipe context.");
			}
			return config;
		}

		protected virtual Task<IBasicConsumer> GetOrCreateConsumerAsync(IPipeContext context, CancellationToken token)
		{
			var consumerTask = ConsumerFunc(ConsumerFactory, token, context);
			if (consumerTask == null)
			{
				_logger.LogWarning("No Consumer creation task found in Pipe context.");
			}
			return consumerTask;
		}
	}
}