import { useState, useEffect } from "react";
import {
    TextInput,
    MultiSelect,
    Button,
    Stack,
    Group,
    Collapse,
    Paper,
    Select,
    Chip,
    Divider,
    Title,
    Text,
} from "@mantine/core";

import type { SearchableFieldInfo } from "@/hooks/swagger-hooks";

export function DynamicFilterPanel({
    fields,
    onApply,
}: {
    fields: SearchableFieldInfo[];
    onApply: (filterState: { [field: string]: any }) => void;
}) {
    const [open, setOpen] = useState(false);
    // For string fields: array of values; for enum: array of selected enums
    const [filterState, setFilterState] = useState<{ [field: string]: string[] }>({});
    const [selectedField, setSelectedField] = useState<string | null>(null);
    const [inputValue, setInputValue] = useState<string>("");

    // Partition fields
    const stringFields = fields.filter(f => f.type === "string" && !f.enum);
    const enumFields = fields.filter(f => !!f.enum);

    // Call onApply whenever filterState changes
    useEffect(() => {
        onApply(filterState);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [filterState]);

    // Add a filter value for string fields
    const handleAddStringFilter = () => {
        if (!selectedField) return;
        const prev = filterState[selectedField] || [];
        if (inputValue && !prev.includes(inputValue)) {
            setFilterState((prevState) => ({
                ...prevState,
                [selectedField]: [...prev, inputValue],
            }));
        }
        setInputValue("");
    };

    // Remove a single value for a string field
    const handleRemoveStringValue = (field: string, value: string) => {
        setFilterState((prev) => {
            const arr = prev[field] || [];
            const next = arr.filter((v: string) => v !== value);
            if (next.length === 0) {
                const { [field]: _, ...rest } = prev;
                return rest;
            }
            return { ...prev, [field]: next };
        });
    };

    // Update enum field values
    const handleEnumChange = (field: string, values: string[]) => {
        setFilterState((prev) => ({
            ...prev,
            [field]: values,
        }));
    };

    const handleClearAll = () => {
        setFilterState({});
    };

    return (
        <>
            <Group mb="md">
                <Button
                    leftSection={<span role="img" aria-label="filter">🔍</span>}
                    variant={open ? "filled" : "outline"}
                    onClick={() => setOpen((v) => !v)}
                >
                    {open ? "Hide Filters" : "Show Filters"}
                </Button>
                {Object.keys(filterState).length > 0 && (
                    <Button size="xs" variant="light" color="gray" onClick={handleClearAll}>
                        Clear all
                    </Button>
                )}
            </Group>
            <Collapse in={open}>
                <Paper withBorder shadow="xs" p="md" mb="md">
                    <Stack gap="md">
                        {/* TEXT FIELDS */}
                        <Title order={5}>Filters</Title>
                        <Group align="end">
                            <Select
                                label="Select field"
                                placeholder="Select field"
                                data={stringFields.map((f) => ({
                                    value: f.name,
                                    label: capitalize(f.name),
                                }))}
                                value={selectedField}
                                onChange={(value) => {
                                    setSelectedField(value);
                                    setInputValue("");
                                }}
                                searchable
                                allowDeselect
                                style={{ minWidth: 180 }}
                            />
                            <TextInput
                                label={selectedField ? `Filter by ${selectedField}` : "Filter value"}
                                value={inputValue}
                                onChange={(e) => setInputValue(e.target.value)}
                                onKeyDown={(e) => {
                                    if (e.key === "Enter") handleAddStringFilter();
                                }}
                                disabled={!selectedField}
                                style={{ minWidth: 200 }}
                            />
                            <Button
                                disabled={
                                    !selectedField ||
                                    !inputValue ||
                                    (filterState[selectedField]?.includes(inputValue) ?? false)
                                }
                                onClick={handleAddStringFilter}
                            >
                                Add filter
                            </Button>
                        </Group>
                        {/* Chips for string fields */}
                        <Group>
                            {stringFields.flatMap((field) =>
                                (filterState[field.name] || []).map((val) => (
                                    <Chip
                                        key={field.name + val}
                                        checked
                                        onChange={() => handleRemoveStringValue(field.name, val)}
                                        color="blue"
                                        variant="filled"
                                    >
                                        {capitalize(field.name)}: {val}
                                    </Chip>
                                ))
                            )}
                        </Group>

                        <Divider my="sm" />

                        {/* ENUM FIELDS */}
                        {enumFields.map((field) => (
                            <div key={field.name}>
                                <Group align="center" >
                                    <Text>
                                        {capitalize(field.name)}
                                    </Text>
                                    <MultiSelect
                                        data={field.enum!.map((v: string) => ({
                                            value: v,
                                            label: v,
                                        }))}
                                        value={filterState[field.name] || []}
                                        onChange={(values) => handleEnumChange(field.name, values)}
                                        placeholder={`Select ${field.name}`}
                                        clearable
                                        searchable
                                        style={{ minWidth: 200 }}
                                    />
                                </Group>
                            </div>
                        ))}
                    </Stack>
                </Paper>
            </Collapse>
        </>
    );
}

function capitalize(str: string) {
    return str.charAt(0).toUpperCase() + str.slice(1);
}
