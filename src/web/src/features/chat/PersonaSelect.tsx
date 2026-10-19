import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { Persona } from "@/lib/api";

export function PersonaSelect({
  personas,
  value,
  onChange,
}: {
  personas: Persona[];
  value: string;
  onChange: (id: string) => void;
}) {
  return (
    <div className="flex items-center gap-2">
      <label htmlFor="persona" className="text-muted-foreground text-sm max-sm:sr-only">
        Ask as
      </label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id="persona" className="w-64 max-w-[60vw]">
          <SelectValue placeholder="Choose a persona" />
        </SelectTrigger>
        <SelectContent>
          {personas.map((persona) => (
            <SelectItem key={persona.id} value={persona.id}>
              {persona.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
