import { Projects } from '@/components/Projects'
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: RouteComponent,
})

function RouteComponent() {
  return (<>
    <Projects />
  </>)
}
