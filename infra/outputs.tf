# Lo que preprod deja a la vista. La cadena de conexión NO sale por acá: un output se imprime en la
# consola y en los logs, y la contraseña del rol es un dato sensible.

output "db_host" {
  value = neon_project.preprod.database_host
}

output "database" {
  value = neon_database.preprod.name
}

output "api_url" {
  value = render_web_service.api.url
}

output "front_url" {
  value = render_web_service.front.url
}
