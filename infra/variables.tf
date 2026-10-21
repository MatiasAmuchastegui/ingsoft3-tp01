# Lo configurable de preprod. Nada de esto es secreto —identificadores, una región, una versión—,
# así que va con su valor por defecto en un archivo que se versiona.

variable "neon_org_id" {
  description = "La organización de Neon donde se crea el proyecto."
  default     = "org-little-band-26455939"
}

variable "neon_region" {
  description = "La región de la base: la misma que la de producción."
  default     = "aws-sa-east-1"
}

variable "pg_version" {
  description = "La versión de Postgres: la misma que la de producción."
  default     = 18
}

variable "imagen_api" {
  description = "La imagen de la api en el registry, SIN etiqueta."
  default     = "ghcr.io/matiasamuchastegui/ingsoft3-tp01-backend"
}

variable "imagen_front" {
  description = "La imagen del front en el registry, SIN etiqueta."
  default     = "ghcr.io/matiasamuchastegui/ingsoft3-tp01-frontend"
}

variable "etiqueta" {
  description = "Qué imagen corre preprod: la de la release v7.0.0, la misma que está en producción."
  default     = "sha-24c876afbd4a45ec09a9fe9f05706dde61a0a1a8"
}

variable "region" {
  description = "La región de Render: la misma de los servicios de QA y producción."
  default     = "oregon"
}
