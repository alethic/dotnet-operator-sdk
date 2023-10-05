﻿using System.CommandLine;
using System.CommandLine.Invocation;

using k8s;
using k8s.Models;

using KubeOps.Abstractions.Kustomize;
using KubeOps.Cli.Output;

using Spectre.Console;

namespace KubeOps.Cli.Commands.Generator;

internal static class OperatorGenerator
{
    public static Command Command
    {
        get
        {
            var cmd = new Command("operator", "Generates deployments and other resources for the operator to run.")
            {
                Options.OutputFormat, Options.OutputPath,
            };
            cmd.AddAlias("op");
            cmd.SetHandler(ctx => Handler(AnsiConsole.Console, ctx));

            return cmd;
        }
    }

    internal static async Task Handler(IAnsiConsole console, InvocationContext ctx)
    {
        var outPath = ctx.ParseResult.GetValueForOption(Options.OutputPath);
        var format = ctx.ParseResult.GetValueForOption(Options.OutputFormat);

        var result = new ResultOutput(console, format);
        console.WriteLine("Generate operator resources.");

        var deployment = new V1Deployment(metadata: new V1ObjectMeta(
            labels: new Dictionary<string, string> { { "operator-deployment", "kubernetes-operator" } },
            name: "operator")).Initialize();
        deployment.Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            RevisionHistoryLimit = 0,
            Selector = new V1LabelSelector(
                matchLabels: new Dictionary<string, string> { { "operator-deployment", "kubernetes-operator" } }),
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta(
                    labels: new Dictionary<string, string> { { "operator-deployment", "kubernetes-operator" } }),
                Spec = new V1PodSpec
                {
                    TerminationGracePeriodSeconds = 10,
                    Containers = new List<V1Container>
                    {
                        new()
                        {
                            Image = "operator",
                            Name = "operator",
                            Env = new List<V1EnvVar>
                            {
                                new()
                                {
                                    Name = "POD_NAMESPACE",
                                    ValueFrom =
                                        new V1EnvVarSource
                                        {
                                            FieldRef = new V1ObjectFieldSelector
                                            {
                                                FieldPath = "metadata.namespace",
                                            },
                                        },
                                },
                            },
                            Resources = new V1ResourceRequirements
                            {
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    { "cpu", new ResourceQuantity("100m") },
                                    { "memory", new ResourceQuantity("64Mi") },
                                },
                                Limits = new Dictionary<string, ResourceQuantity>
                                {
                                    { "cpu", new ResourceQuantity("100m") },
                                    { "memory", new ResourceQuantity("128Mi") },
                                },
                            },
                        },
                    },
                },
            },
        };
        result.Add($"deployment.{format.ToString().ToLowerInvariant()}", deployment);

        result.Add(
            $"kustomization.{format.ToString().ToLowerInvariant()}",
            new KustomizationConfig
            {
                Resources = new List<string> { $"deployment.{format.ToString().ToLowerInvariant()}", },
                CommonLabels = new Dictionary<string, string> { { "operator-element", "operator-instance" }, },
            });

        if (outPath is not null)
        {
            await result.Write(outPath);
        }
        else
        {
            result.Write();
        }
    }
}
