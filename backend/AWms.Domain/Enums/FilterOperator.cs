namespace AWms.Domain.Enums;

public enum FilterOperator
{
    eq,
    neq,
    contains,
    startsWith,
    gt,
    gte,
    lt,
    lte,
    between,
    isNull,
    isNotNull,
    @in,
    notIn
}
