﻿using System;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Exceptions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Microsoft.Data.OData.Query.SemanticAst;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public abstract class CloudFormationInstallationConventionBase: IInstallConvention
    {
        protected readonly StackEventLogger Logger;

        public CloudFormationInstallationConventionBase(StackEventLogger logger)
        {
            Logger = logger;
        }
        
        public abstract void Install(RunningDeployment deployment);
        
        /// <summary>
        /// The AmazonServiceException can hold additional information that is useful to include in
        /// the log.
        /// </summary>
        /// <param name="exception">The exception</param>
        protected void HandleAmazonServiceException(AmazonServiceException exception)
        {
            exception.GetWebExceptionMessage()
                .Tee(message => DisplayWarning("AWS-CLOUDFORMATION-ERROR-0014", message));
        }

        /// <summary>
        /// Run an action and log any AmazonServiceException detail.
        /// </summary>
        /// <param name="func">The exception</param>
        protected T WithAmazonServiceExceptionHandling<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (AmazonServiceException exception)
            {
                HandleAmazonServiceException(exception);
                throw;
            }
        }

        /// <summary>
        /// Run an action and log any AmazonServiceException detail.
        /// </summary>
        /// <param name="action">The action to invoke</param>
        protected void WithAmazonServiceExceptionHandling(Action action)
        {
            WithAmazonServiceExceptionHandling<ValueTuple>(() => default(ValueTuple).Tee(x=> action()));
        }
        
        
        /// <summary>
        /// Display an warning message to the user (without duplicates)
        /// </summary>
        /// <param name="errorCode">The error message code</param>
        /// <param name="message">The error message body</param>
        /// <returns>true if it was displayed, and false otherwise</returns>
        protected bool DisplayWarning(string errorCode, string message)
        {
            return Logger.Warn(errorCode, message);
        }
        
        /// <summary>
        /// Creates a handler which will log stack events and throw on common rollback events
        /// </summary>
        /// <param name="clientFactory">The client factory</param>
        /// <param name="stack">The stack to query</param>
        /// <param name="filter">The filter for stack events</param>
        /// <param name="expectSuccess">Whether we expected a success</param>
        /// <param name="missingIsFailure"></param>
        /// <returns>Stack event handler</returns>
        protected Action<Maybe<StackEvent>> LogAndThrowRollbacks(Func<IAmazonCloudFormation> clientFactory, StackArn stack, bool expectSuccess = true, bool missingIsFailure = true, Func<StackEvent, bool> filter = null) 
        {
            return @event =>
            {
                try
                {
                    Logger.Log(@event);
                    Logger.LogRollbackError(@event, x => 
                        WithAmazonServiceExceptionHandling(() => clientFactory.GetLastStackEvent(stack, x)), 
                        expectSuccess, 
                        missingIsFailure);
                }
                catch (PermissionException exception)
                {
                    Log.Warn(exception.Message);
                }
            };
        }

        protected void SetOutputVariable(CalamariVariableDictionary variables, string name, string value)
        {
            Log.SetOutputVariable($"AwsOutputs[{name}]", value ?? "", variables);
            Log.Info($"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.AwsOutputs[{name}]\"");
        }

        protected Stack QueryStack(Func<IAmazonCloudFormation> clientFactory, StackArn stack)
        {
            try
            {
                return clientFactory.DescribeStack(stack);
            }
            catch (AmazonServiceException ex)
            {
                if (ex.ErrorCode == "AccessDenied")
                {
                    throw new PermissionException(
                        "AWS-CLOUDFORMATION-ERROR-0004: The AWS account used to perform the operation does not have " +
                        "the required permissions to describe the CloudFormation stack. " +
                        "This means that the step is not able to generate any output variables.\n" +
                        ex.Message + "\n" +
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0004",
                        ex);
                }

                throw new UnknownException(
                    "AWS-CLOUDFORMATION-ERROR-0005: An unrecognised exception was thrown while querying the CloudFormation stacks.\n" +
                    "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-cloudformation-error-0005",
                    ex);
            }
        }
    }
}