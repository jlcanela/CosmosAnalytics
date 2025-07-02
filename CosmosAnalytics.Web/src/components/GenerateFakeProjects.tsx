import { useState } from "react";
import {
  Title,
  Card,
  Slider,
  Button,
  Group,
  Text,
  Stack,
  Notification,
  Loader,
} from "@mantine/core";
import { IconCheck, IconX } from "@tabler/icons-react";
import createFetchClient from "openapi-fetch";
import createClient from "openapi-react-query";
import type { paths } from "../schema-api";

const fetchClient = createFetchClient<paths>({
  baseUrl: "https://localhost:7415",
});
const $api = createClient(fetchClient);

export function GenerateFakeProjects() {
  const [size, setSize] = useState(10);
  const [notification, setNotification] = useState<{
    message: string;
    color: "green" | "red";
    icon: React.ReactNode;
  } | null>(null);

  // React Query v4: isLoading; v5: isPending
  const { mutate, status } = $api.useMutation("post", "/api/generate-sample");

  const handleGenerate = () => {
    mutate(
      { params: { query: { size } } },
      {
        onSuccess: (data: number) => {
          setNotification({
            message: `Successfully generated ${data} fake project(s)!`,
            color: "green",
            icon: <IconCheck size={18} />,
          });
        },
        onError: (error: any) => {
          setNotification({
            message:
              error?.message ||
              "An error occurred while generating fake projects.",
            color: "red",
            icon: <IconX size={18} />,
          });
        },
      }
    );
  };

  return (
    <Stack mx="auto" my="xl" maw={600}>
      <Title order={1}>Administration Page</Title>
      <Card shadow="sm" padding="lg" radius="md" withBorder>
        <Title order={3} mb="md">
          Generate Fake Projects
        </Title>
        <Text mb="xs">
          Use the slider to select how many fake projects to generate (0–100).
        </Text>
        <Group align="flex-end">
          <Slider
            min={0}
            max={10000}
            value={size}
            onChange={setSize}
            marks={[
              { value: 0, label: "0" },
              { value: 1000, label: "1000" },
              { value: 5000, label: "5000" },
              { value: 10000, label: "10000" },
            ]}
            style={{ flex: 1, marginBottom: 20 }}
          />
          <Text size="lg" fw={700} style={{ width: 40, textAlign: "center" }}>
            {size}
          </Text>
        </Group>
        <Button
          mt="md"
          onClick={handleGenerate}
          disabled={status === "pending" || size === 0}
          leftSection={status === "pending" ? <Loader size={16} /> : undefined}
        >
          {status === "pending" ? "Generating..." : "Generate"}
        </Button>
        {notification && (
          <Notification
            color={notification.color}
            icon={notification.icon}
            mt="md"
            onClose={() => setNotification(null)}
            withCloseButton
          >
            {notification.message}
          </Notification>
        )}
      </Card>
    </Stack>
  );
}
