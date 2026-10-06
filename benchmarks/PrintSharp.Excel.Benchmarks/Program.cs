using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PrintSharp.Excel;

BenchmarkSwitcher.FromAssembly(typeof(TemplateResolverBenchmarks).Assembly).Run(args);

[GenerateTemplateBindings]
public sealed class BenchmarkInvoiceData
{
    public BenchmarkCustomerData Customer { get; init; } = new();
}

public sealed class BenchmarkCustomerData
{
    public string Name { get; init; } = "Taro";
}

[MemoryDiagnoser]
public class TemplateResolverBenchmarks
{
    private readonly ReflectionValueResolver _reflectionResolver = new();
    private readonly DictionaryValueResolver _dictionaryResolver = new();
    private readonly BenchmarkInvoiceDataTemplateValueResolver _generatedResolver = new();
    private readonly CachedReflectionResolver _cachedReflectionResolver = new();
    private readonly DataSetTemplateValueResolver _dataSetResolver = new();

    private readonly BenchmarkInvoiceData _pocoData = new();
    private readonly Dictionary<string, object?> _dictionaryData = new()
    {
        ["Customer"] = new Dictionary<string, object?> { ["Name"] = "Taro" }
    };
    private readonly DataSet _dataSet = CreateDataSet();

    [Params(10_000, 100_000, 1_000_000)]
    public int BindingCount { get; set; }

    [Benchmark(Baseline = true)]
    public int Reflection() => ResolveRepeatedly(_reflectionResolver, _pocoData);

    [Benchmark]
    public int CachedReflection() => ResolveRepeatedly(_cachedReflectionResolver, _pocoData);

    [Benchmark]
    public int Dictionary() => ResolveRepeatedly(_dictionaryResolver, _dictionaryData);

    [Benchmark]
    public int Generated() => ResolveRepeatedly(_generatedResolver, _pocoData);

    [Benchmark]
    public int DataSet() => ResolveRepeatedly(_dataSetResolver, _dataSet, "Items.Name");

    [Benchmark]
    public int DataTable() => ResolveRepeatedly(_dataSetResolver, _dataSet.Tables["Items"]!, "Name");

    private int ResolveRepeatedly(ITemplateValueResolver resolver, object data, string path = "Customer.Name")
    {
        var result = 0;
        for (var index = 0; index < BindingCount; index++)
        {
            result += resolver.Resolve(data, path)?.ToString()?.Length ?? 0;
        }

        return result;
    }

    private static DataSet CreateDataSet()
    {
        var dataSet = new DataSet();
        var table = new DataTable("Items");
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add("Taro");
        dataSet.Tables.Add(table);
        return dataSet;
    }

    private sealed class CachedReflectionResolver : ITemplateValueResolver
    {
        private readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo> _properties = new();

        public object? Resolve(object? data, string path)
        {
            if (data is null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            object? current = data;
            foreach (var segment in path.Split('.'))
            {
                if (current is null)
                {
                    return null;
                }

                var type = current.GetType();
                var property = _properties.GetOrAdd((type, segment), key =>
                    key.Type.GetProperty(key.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new MissingMemberException(key.Type.FullName, key.Name));
                current = property.GetValue(current);
            }

            return current;
        }
    }
}
