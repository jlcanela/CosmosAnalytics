import { createFileRoute, Link } from "@tanstack/react-router";

export const Route = createFileRoute("/about")({
  component: About,
});

function About() {
  return (
    <div>

      <Link to="/" className="[&.active]:font-bold">
        About
      </Link>{" "}
    </div>
  );
}